using System.Security.Claims;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.EmployeeLedger;
using EngineeringManager.Application.Employees;
using EngineeringManager.Application.Organization;
using EngineeringManager.Application.Partners;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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
    IBusinessPartnerService partnerService) : PageModel
{
    public EmployeeDto Employee { get; private set; } = null!;
    public EmployeeAnnualLedgerDto? Ledger { get; private set; }
    public IReadOnlyList<EmployeeWageEntryDto> WageEntries { get; private set; } = [];
    public IReadOnlyList<EmployeeExpenseDto> Expenses { get; private set; } = [];
    public IReadOnlyList<EmployeeOtherPayableDto> OtherPayables { get; private set; } = [];
    public IReadOnlyList<BusinessYearDto> BusinessYears { get; private set; } = [];
    public IReadOnlyList<EmployeeCertificateDto> Certificates { get; private set; } = [];
    public IReadOnlyList<LegalEntityDto> LegalEntities { get; private set; } = [];
    public IReadOnlyList<ProjectListItemDto> Projects { get; private set; } = [];
    public IReadOnlyList<BusinessPartnerDto> LaborPartners { get; private set; } = [];
    public bool CanViewSensitive => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
    public bool CanEditFinancial => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
    public bool CanManageEmployee => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public string PrimaryPayMethod => Employee.DefaultMonthlySalary.HasValue ? "月薪" : Employee.DefaultDailyRate.HasValue ? "计日" : Employee.DefaultHourlyRate.HasValue ? "计时" : Employee.DefaultPieceworkRate.HasValue ? "计件" : "未设置";

    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessYearId { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "wages";
    [BindProperty(SupportsGet = true)] public string WageSubtab { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string PaymentSubtab { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string DividendSubtab { get; set; } = "all";
    [BindProperty] public WageEntryInput WageInput { get; set; } = new();
    [BindProperty] public ExpenseEntryInput ExpenseInput { get; set; } = new();
    [BindProperty] public OtherPayableInput OtherInput { get; set; } = new();
    [BindProperty] public EmployeeEditInput EmployeeInput { get; set; } = new();
    [BindProperty] public WageEditInput WageEdit { get; set; } = new();
    [BindProperty] public ExpenseEditInput ExpenseEdit { get; set; } = new();
    [BindProperty] public OtherEditInput OtherEdit { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken) ? Page() : NotFound();

    public Task<IActionResult> OnPostUpdateEmployeeAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        if (!CanManageEmployee) throw new UnauthorizedAccessException();
        var current = await employeeService.GetAsync(Id, cancellationToken) ?? throw new InvalidOperationException("员工不存在。");
        await employeeService.UpdateAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
            new UpdateEmployeeRequest(
                Id,
                EmployeeInput.EmployeeNumber,
                EmployeeInput.Name,
                EmployeeInput.EmployeeType,
                EmployeeInput.Phone,
                EmployeeInput.IdentityNumber,
                EmployeeInput.BankAccountNumber,
                EmployeeInput.BankName,
                EmployeeInput.HireDate,
                EmployeeInput.LeaveDate,
                EmployeeInput.PositionTitle,
                EmployeeInput.DefaultLegalEntityId,
                EmployeeInput.DefaultMonthlySalary,
                EmployeeInput.DefaultDailyRate,
                EmployeeInput.DefaultHourlyRate,
                EmployeeInput.DefaultPieceworkRate,
                EmployeeInput.IsActive,
                EmployeeInput.ConcurrencyStamp,
                EmployeeInput.Reason,
                current.Notes),
            cancellationToken);
    }, Tab, cancellationToken);

    public Task<IActionResult> OnPostAddWageAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
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
                WageInput.Notes,
                WageInput.EntryType,
                await ReadAttachmentAsync(WageInput.Attachment, cancellationToken)),
            cancellationToken);
    }, "wages", cancellationToken);

    public Task<IActionResult> OnPostUpdateWageAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        EnsureFinancialEditor();
        await annualLedgerService.UpdateWageEntryAsync(
            new UpdateEmployeeWageEntryRequest(
                WageEdit.Id,
                WageEdit.ConcurrencyStamp,
                WageEdit.StartDate,
                WageEdit.EndDate,
                WageEdit.EntryType,
                WageEdit.WageCategory,
                WageEdit.CalculationMethod,
                WageEdit.Nature,
                WageEdit.Quantity,
                WageEdit.Unit,
                WageEdit.UnitPrice,
                WageEdit.ManualAmount,
                WageEdit.LegalEntityId,
                WageEdit.ProjectId,
                WageEdit.LaborBusinessPartnerId,
                WageEdit.AdjustmentAmount,
                WageEdit.Notes,
                await ReadAttachmentAsync(WageEdit.Attachment, cancellationToken),
                WageEdit.Reason,
                User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
    }, "wages", cancellationToken);

    public Task<IActionResult> OnPostAddExpenseAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        EnsureFinancialEditor();
        await employeeLedgerService.CreateExpenseAsync(
            new CreateExpenseRequest(
                Id,
                ExpenseInput.ProjectId,
                null,
                ExpenseInput.LegalEntityId,
                ExpenseInput.ExpenseDate,
                "报销",
                ExpenseInput.Amount,
                ExpenseInput.Notes,
                0m,
                ExpenseInput.ReceiptNumber,
                await ReadExpenseAttachmentAsync(ExpenseInput.Attachment, cancellationToken)),
            cancellationToken);
    }, "expenses", cancellationToken);

    public Task<IActionResult> OnPostUpdateExpenseAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        EnsureFinancialEditor();
        await employeeLedgerService.UpdateExpenseAsync(
            new UpdateExpenseRequest(
                ExpenseEdit.Id,
                ExpenseEdit.ConcurrencyStamp,
                ExpenseEdit.ExpenseDate,
                ExpenseEdit.Amount,
                ExpenseEdit.ProjectId,
                ExpenseEdit.ReceiptNumber,
                await ReadExpenseAttachmentAsync(ExpenseEdit.Attachment, cancellationToken),
                ExpenseEdit.Description,
                ExpenseEdit.Reason,
                User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
    }, "expenses", cancellationToken);

    public Task<IActionResult> OnPostAddOtherPayableAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        EnsureFinancialEditor();
        await employeeLedgerService.CreateOtherPayableAsync(
            new CreateEmployeeOtherPayableRequest(Id, OtherInput.ProjectId, OtherInput.LegalEntityId, OtherInput.EntryDate, OtherInput.Amount, OtherInput.EntryType, OtherInput.Description),
            cancellationToken);
    }, "dividends", cancellationToken);

    public Task<IActionResult> OnPostUpdateOtherPayableAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        EnsureFinancialEditor();
        await employeeLedgerService.UpdateOtherPayableAsync(
            new UpdateEmployeeOtherPayableRequest(
                OtherEdit.Id,
                OtherEdit.ConcurrencyStamp,
                OtherEdit.EntryDate,
                OtherEdit.Amount,
                OtherEdit.EntryType,
                OtherEdit.LegalEntityId,
                OtherEdit.ProjectId,
                OtherEdit.Description,
                OtherEdit.Reason,
                User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
    }, "dividends", cancellationToken);

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetAsync(Id, cancellationToken);
        if (employee is null) return false;
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
            WageEntries = await annualLedgerService.GetWageEntriesAsync(Id, selectedYear.Id, cancellationToken);
        }

        Expenses = await employeeLedgerService.GetExpensesAsync(Id, cancellationToken);
        OtherPayables = await employeeLedgerService.GetOtherPayablesAsync(Id, cancellationToken);
        Certificates = await certificateService.ListAsync(new CertificateFilter(OwnerId: Id), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        var organization = await organizationService.GetOverviewAsync(cancellationToken);
        LegalEntities = organization.LegalEntities.Where(item => item.IsActive).ToArray();
        Projects = await projectService.ListProjectsAsync(null, null, cancellationToken);
        LaborPartners = await partnerService.ListAsync(null, BusinessPartnerRoleType.ConstructionCrew, cancellationToken);
        return true;
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task> action, string targetTab, CancellationToken cancellationToken)
    {
        try
        {
            await action();
            return RedirectToPage(new { id = Id, businessYearId = BusinessYearId, tab = targetTab, wageSubtab = WageSubtab, paymentSubtab = PaymentSubtab, dividendSubtab = DividendSubtab });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Tab = targetTab;
            return await LoadAsync(cancellationToken) ? Page() : NotFound();
        }
    }

    private void EnsureFinancialEditor()
    {
        if (!CanEditFinancial) throw new UnauthorizedAccessException();
    }

    private Guid RequireBusinessYear() => BusinessYearId ?? throw new InvalidOperationException("请先配置并选择业务年度。");

    private static async Task<EmployeePayableAttachmentUpload?> ReadAttachmentAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return null;
        await using var source = file.OpenReadStream();
        using var content = new MemoryStream();
        await source.CopyToAsync(content, cancellationToken);
        return new EmployeePayableAttachmentUpload(file.FileName, file.ContentType, content.ToArray());
    }

    private static async Task<ExpenseAttachmentUpload?> ReadExpenseAttachmentAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return null;
        await using var source = file.OpenReadStream();
        using var content = new MemoryStream();
        await source.CopyToAsync(content, cancellationToken);
        return new ExpenseAttachmentUpload(file.FileName, file.ContentType, content.ToArray());
    }

    public sealed class EmployeeEditInput
    {
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public EmployeeType EmployeeType { get; set; } = EmployeeType.Formal;
        public string? Phone { get; set; }
        public string? IdentityNumber { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankName { get; set; }
        public DateOnly? HireDate { get; set; }
        public DateOnly? LeaveDate { get; set; }
        public string? PositionTitle { get; set; }
        public Guid? DefaultLegalEntityId { get; set; }
        public decimal? DefaultMonthlySalary { get; set; }
        public decimal? DefaultDailyRate { get; set; }
        public decimal? DefaultHourlyRate { get; set; }
        public decimal? DefaultPieceworkRate { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "快捷编辑员工资料";
    }

    public class WageEntryInput
    {
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public EmployeeWageEntryType EntryType { get; set; } = EmployeeWageEntryType.Attendance;
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
        public IFormFile? Attachment { get; set; }
    }

    public sealed class WageEditInput : WageEntryInput
    {
        public Guid Id { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "修改工资明细";
    }

    public sealed class ExpenseEntryInput
    {
        public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public decimal Amount { get; set; }
        public Guid LegalEntityId { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ReceiptNumber { get; set; }
        public IFormFile? Attachment { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class ExpenseEditInput
    {
        public Guid Id { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public DateOnly ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ReceiptNumber { get; set; }
        public IFormFile? Attachment { get; set; }
        public string? Description { get; set; }
        public string Reason { get; set; } = "修改报销金额";
    }

    public sealed class OtherPayableInput
    {
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public EmployeeLedgerEntryType EntryType { get; set; } = EmployeeLedgerEntryType.Dividend;
        public decimal Amount { get; set; }
        public Guid LegalEntityId { get; set; }
        public Guid? ProjectId { get; set; }
        public string? Description { get; set; }
    }

    public sealed class OtherEditInput
    {
        public Guid Id { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        public DateOnly EntryDate { get; set; }
        public decimal Amount { get; set; }
        public EmployeeLedgerEntryType EntryType { get; set; }
        public Guid LegalEntityId { get; set; }
        public Guid? ProjectId { get; set; }
        public string? Description { get; set; }
        public string Reason { get; set; } = "修改利息分红";
    }
}
