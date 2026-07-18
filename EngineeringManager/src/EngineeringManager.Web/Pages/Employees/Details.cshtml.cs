using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.EmployeeLedger;
using EngineeringManager.Application.Employees;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Organization;
using EngineeringManager.Application.Partners;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Employees;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class DetailsModel(
    IEmployeeService employeeService,
    IBusinessYearService businessYearService,
    IEmployeeAnnualLedgerService annualLedgerService,
    IEmployeeLedgerService employeeLedgerService,
    IEmployeeCertificateService certificateService,
    IOrganizationService organizationService,
    IProjectService projectService,
    IBusinessPartnerService partnerService,
    IFinanceLedgerService financeService) : PageModel
{
    public EmployeeDto Employee { get; private set; } = null!;
    public EmployeeAnnualLedgerDto? Ledger { get; private set; }
    public IReadOnlyList<BusinessYearDto> BusinessYears { get; private set; } = [];
    public IReadOnlyList<EmployeeCertificateDto> Certificates { get; private set; } = [];
    public IReadOnlyList<LegalEntityDto> LegalEntities { get; private set; } = [];
    public IReadOnlyList<ProjectListItemDto> Projects { get; private set; } = [];
    public IReadOnlyList<BusinessPartnerDto> LaborPartners { get; private set; } = [];
    public IReadOnlyList<FinancialAccountDto> Accounts { get; private set; } = [];
    public bool CanViewSensitive => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
    public bool CanEditFinancial => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
    public bool CanAdjust => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public string PrimaryPayMethod => Employee.DefaultMonthlySalary.HasValue ? "月薪" : Employee.DefaultDailyRate.HasValue ? "计日" : Employee.DefaultHourlyRate.HasValue ? "计时" : Employee.DefaultPieceworkRate.HasValue ? "计件" : "未设置";

    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessYearId { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "wages";
    [BindProperty] public WageEntryInput WageInput { get; set; } = new();
    [BindProperty] public ExpenseEntryInput ExpenseInput { get; set; } = new();
    [BindProperty] public OtherPayableInput OtherInput { get; set; } = new();
    [BindProperty] public ReceiptEntryInput ReceiptInput { get; set; } = new();
    [BindProperty] public AdjustmentEntryInput AdjustmentInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await LoadAsync(cancellationToken) ? Page() : NotFound();
    }

    public Task<IActionResult> OnPostAddWageAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () =>
            {
                EnsureFinancialEditor();
                await annualLedgerService.AddWageEntryAsync(
                    new CreateEmployeeWageEntryRequest(
                        Id,
                        RequireBusinessYear(),
                        WageInput.StartDate,
                        WageInput.EndDate,
                        WageInput.WageCategory,
                        WageInput.CalculationMethod,
                        WageInput.Nature,
                        WageInput.Quantity,
                        WageInput.Unit,
                        WageInput.UnitPrice,
                        WageInput.ManualAmount,
                        WageInput.LegalEntityId,
                        WageInput.ProjectId,
                        WageInput.LaborBusinessPartnerId,
                        WageInput.AdjustmentAmount,
                        WageInput.Notes),
                    cancellationToken);
            },
            "wages",
            cancellationToken);

    public Task<IActionResult> OnPostAddExpenseAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () =>
            {
                EnsureFinancialEditor();
                ExpenseAttachmentUpload? attachment = null;
                if (ExpenseInput.Attachment is not null)
                {
                    await using var source = ExpenseInput.Attachment.OpenReadStream();
                    using var content = new MemoryStream();
                    await source.CopyToAsync(content, cancellationToken);
                    attachment = new ExpenseAttachmentUpload(
                        ExpenseInput.Attachment.FileName,
                        ExpenseInput.Attachment.ContentType,
                        content.ToArray());
                }

                await employeeLedgerService.CreateExpenseAsync(
                    new CreateExpenseRequest(
                        Id,
                        ExpenseInput.ProjectId,
                        ExpenseInput.DepartmentId,
                        ExpenseInput.LegalEntityId,
                        ExpenseInput.ExpenseDate,
                        ExpenseInput.Category,
                        ExpenseInput.OriginalAmount,
                        ExpenseInput.Notes,
                        ExpenseInput.AdjustmentAmount,
                        ExpenseInput.ReceiptNumber,
                        attachment),
                    cancellationToken);
            },
            "expenses",
            cancellationToken);

    public Task<IActionResult> OnPostAddOtherPayableAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () =>
            {
                EnsureFinancialEditor();
                await employeeLedgerService.CreateOtherPayableAsync(
                    new CreateEmployeeOtherPayableRequest(
                        Id,
                        OtherInput.ProjectId,
                        OtherInput.LegalEntityId,
                        OtherInput.EntryDate,
                        OtherInput.Amount,
                        OtherInput.EntryType,
                        OtherInput.Notes),
                    cancellationToken);
            },
            "other",
            cancellationToken);

    public Task<IActionResult> OnPostAddReceiptAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () =>
            {
                EnsureFinancialEditor();
                await annualLedgerService.RecordReceiptAsync(
                    new RecordEmployeeReceiptRequest(
                        Id,
                        RequireBusinessYear(),
                        ReceiptInput.ReceiptDate,
                        ReceiptInput.ReceiptType,
                        ReceiptInput.Amount,
                        ReceiptInput.PaymentLegalEntityId,
                        ReceiptInput.AccountId,
                        ReceiptInput.PaymentMethod,
                        ReceiptInput.ActualRecipientName,
                        ReceiptInput.ProjectId,
                        ReceiptInput.LaborBusinessPartnerId,
                        ReceiptInput.Notes),
                    cancellationToken);
            },
            "receipts",
            cancellationToken);

    public Task<IActionResult> OnPostAddAdjustmentAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () =>
            {
                if (!CanAdjust)
                {
                    throw new UnauthorizedAccessException();
                }

                await annualLedgerService.AddAdjustmentAsync(
                    new CreateEmployeeFinancialAdjustmentRequest(
                        Id,
                        RequireBusinessYear(),
                        AdjustmentInput.AdjustmentDate,
                        AdjustmentInput.Amount,
                        AdjustmentInput.AdjustmentType,
                        AdjustmentInput.Notes),
                    cancellationToken);
            },
            "history",
            cancellationToken);

    public Task<IActionResult> OnPostReverseAdjustmentAsync(
        Guid adjustmentId,
        DateOnly reversalDate,
        string notes,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () =>
            {
                if (!CanAdjust)
                {
                    throw new UnauthorizedAccessException();
                }

                await annualLedgerService.ReverseAdjustmentAsync(adjustmentId, reversalDate, notes, cancellationToken);
            },
            "history",
            cancellationToken);

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetAsync(Id, cancellationToken);
        if (employee is null)
        {
            return false;
        }

        Employee = CanViewSensitive
            ? employee
            : employee with
            {
                IdentityNumber = EmployeeSensitiveDataMasker.MaskIdentityNumber(employee.IdentityNumber),
                BankAccountNumber = EmployeeSensitiveDataMasker.MaskBankAccountNumber(employee.BankAccountNumber)
            };
        BusinessYears = await businessYearService.ListAsync(cancellationToken);
        var selectedYear = BusinessYearId.HasValue
            ? BusinessYears.SingleOrDefault(item => item.Id == BusinessYearId.Value)
            : await businessYearService.GetByDateAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken) ?? (BusinessYears.Count > 0 ? BusinessYears[0] : null);
        if (selectedYear is not null)
        {
            BusinessYearId = selectedYear.Id;
            Ledger = await annualLedgerService.GetAnnualLedgerAsync(Id, selectedYear.Id, cancellationToken);
        }

        Certificates = await certificateService.ListAsync(new CertificateFilter(OwnerId: Id), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        var organization = await organizationService.GetOverviewAsync(cancellationToken);
        LegalEntities = organization.LegalEntities.Where(item => item.IsActive).ToArray();
        Projects = await projectService.ListProjectsAsync(null, null, cancellationToken);
        LaborPartners = await partnerService.ListAsync(null, BusinessPartnerRoleType.ConstructionCrew, cancellationToken);
        Accounts = (await financeService.ListAccountsAsync(cancellationToken)).Where(item => item.IsActive).ToArray();
        return true;
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task> action, string targetTab, CancellationToken cancellationToken)
    {
        try
        {
            await action();
            return RedirectToPage(new { id = Id, businessYearId = BusinessYearId, tab = targetTab });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Tab = targetTab;
            return await LoadAsync(cancellationToken) ? Page() : NotFound();
        }
    }

    private void EnsureFinancialEditor()
    {
        if (!CanEditFinancial)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private Guid RequireBusinessYear() => BusinessYearId ?? throw new InvalidOperationException("请先配置并选择业务年度。");

    public sealed class WageEntryInput
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public EmployeeWageCategory WageCategory { get; set; } = EmployeeWageCategory.OtherWage;
        public EmployeeWageCalculationMethod CalculationMethod { get; set; } = EmployeeWageCalculationMethod.FixedAmount;
        public PayrollItemNature Nature { get; set; } = PayrollItemNature.Earning;
        public decimal? Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? ManualAmount { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? LaborBusinessPartnerId { get; set; }
        public decimal AdjustmentAmount { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class ExpenseEntryInput
    {
        public DateOnly ExpenseDate { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal OriginalAmount { get; set; }
        public decimal AdjustmentAmount { get; set; }
        public Guid LegalEntityId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? ReceiptNumber { get; set; }
        public IFormFile? Attachment { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class OtherPayableInput
    {
        public DateOnly EntryDate { get; set; }
        public EmployeeLedgerEntryType EntryType { get; set; } = EmployeeLedgerEntryType.Dividend;
        public decimal Amount { get; set; }
        public Guid LegalEntityId { get; set; }
        public Guid? ProjectId { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class ReceiptEntryInput
    {
        public DateOnly ReceiptDate { get; set; }
        public EmployeeReceiptType ReceiptType { get; set; } = EmployeeReceiptType.General;
        public decimal Amount { get; set; }
        public Guid PaymentLegalEntityId { get; set; }
        public Guid AccountId { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
        public string ActualRecipientName { get; set; } = string.Empty;
        public Guid? ProjectId { get; set; }
        public Guid? LaborBusinessPartnerId { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class AdjustmentEntryInput
    {
        public DateOnly AdjustmentDate { get; set; }
        public decimal Amount { get; set; }
        public EmployeeFinancialAdjustmentType AdjustmentType { get; set; } = EmployeeFinancialAdjustmentType.AdministratorAdjustment;
        public string Notes { get; set; } = string.Empty;
    }
}
