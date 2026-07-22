using System.Security.Claims;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Web.Presentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Payroll;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance)]
public sealed class EditModel(IPayrollService payrollService, ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? LineId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty] public BatchInput Input { get; set; } = new();
    public IReadOnlyList<SelectOption> Projects { get; private set; } = [];
    public IReadOnlyList<SelectOption> Companies { get; private set; } = [];
    public IReadOnlyList<AccountOption> Accounts { get; private set; } = [];
    public IReadOnlyList<SelectOption> LaborPartners { get; private set; } = [];
    public IEnumerable<AccountOption> CompanyAccounts => Accounts.Where(item => item.Type != FinancialAccountType.PersonalAdvance);
    public IEnumerable<AccountOption> PersonalAdvanceAccounts => Accounts.Where(item => item.Type == FinancialAccountType.PersonalAdvance && item.OwnerEmployeeId.HasValue);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        await LoadInputAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        try
        {
            var existingAllocations = new Dictionary<Guid, PayrollCrewAllocationDto>();
            if (Id.HasValue)
            {
                var details = await payrollService.GetDisbursementBatchAsync(Id.Value, cancellationToken)
                    ?? throw new InvalidOperationException("工资批次不存在。");
                existingAllocations = details.CrewAllocations.ToDictionary(item => item.CrewBusinessPartnerId);
            }
            var lines = Input.EmployeeLines.Where(item => item.Selected && item.Amount > 0m)
                .Select(item => new PayrollDisbursementLineRequest(item.PaymentId, PayrollRecipientType.Employee, item.PersonId, null, null, item.Amount, item.Notes, item.PaymentCategory, item.WageCategory, item.LaborBusinessPartnerId, item.ProjectId))
                .Concat(Input.CrewLines.Where(item => item.Selected && item.Amount > 0m)
                    .Select(item => new PayrollDisbursementLineRequest(item.PaymentId, PayrollRecipientType.CrewWorker, null, item.PersonId, item.CrewBusinessPartnerId, item.Amount, item.Notes, item.PaymentCategory, item.WageCategory, item.LaborBusinessPartnerId ?? item.CrewBusinessPartnerId, item.ProjectId)))
                .ToArray();
            var crewAllocations = lines.Where(item => item.CrewBusinessPartnerId.HasValue).Select(item => item.CrewBusinessPartnerId!.Value).Distinct()
                .Select(crewId => existingAllocations.TryGetValue(crewId, out var existing)
                    ? new PayrollCrewAllocationRequest(crewId, existing.ContractId, existing.PayableEntryId, existing.Notes)
                    : new PayrollCrewAllocationRequest(crewId, null, null, "工程款待关联"))
                .ToArray();
            var saved = await payrollService.SaveDisbursementBatchAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                new SavePayrollDisbursementBatchRequest(
                    Id,
                    Input.BatchNumber,
                    Input.Name,
                    Input.PaymentDate,
                    Input.ProjectId,
                    Input.LegalEntityId,
                    Input.FundingSource == PayrollFundingSource.PersonalAdvance ? Input.PersonalAdvanceAccountId : Input.AccountId,
                    Input.ActualAmount,
                    Input.PaymentMethod,
                    Input.VoucherNumber,
                    Input.Status,
                    Input.Notes,
                    Input.ConcurrencyStamp,
                    Input.Reason,
                    lines,
                    crewAllocations,
                    Input.DisbursementType,
                    Input.FundingSource,
                    Input.RepaysPersonalAdvanceAccountId),
                cancellationToken);
            if (IsLocalReturnUrl(ReturnUrl)) return LocalRedirect(ReturnUrl!);
            return RedirectToPage(new { id = saved.Batch.Id });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await ReloadPersonLinesAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadInputAsync(CancellationToken cancellationToken)
    {
        PayrollDisbursementBatchDetailsDto? details = null;
        if (Id.HasValue)
        {
            details = await payrollService.GetDisbursementBatchAsync(Id.Value, cancellationToken) ?? throw new InvalidOperationException("工资批次不存在。");
            Input.BatchNumber = details.Batch.BatchNumber;
            Input.Name = details.Batch.Name;
            Input.PaymentDate = details.Batch.PaymentDate;
            Input.ProjectId = details.Batch.ProjectId;
            Input.LegalEntityId = details.Batch.LegalEntityId;
            Input.AccountId = details.Batch.FundingSource == PayrollFundingSource.CompanyAccount ? details.Batch.AccountId : null;
            Input.DisbursementType = details.Batch.DisbursementType;
            Input.FundingSource = details.Batch.FundingSource;
            Input.PersonalAdvanceAccountId = details.Batch.FundingSource == PayrollFundingSource.PersonalAdvance ? details.Batch.AccountId : null;
            Input.RepaysPersonalAdvanceAccountId = details.Batch.RepaysPersonalAdvanceAccountId;
            Input.ActualAmount = details.Batch.ActualAmount;
            Input.PaymentMethod = details.Batch.PaymentMethod;
            Input.VoucherNumber = details.Batch.VoucherNumber;
            Input.Status = details.Batch.Status;
            Input.Notes = details.Batch.Notes;
            Input.ConcurrencyStamp = details.Batch.ConcurrencyStamp;
            Input.Reason = "维护工资发放批次";
        }
        else
        {
            Input.PaymentDate = DateOnly.FromDateTime(DateTime.Today);
            Input.Status = PayrollBatchStatus.Draft;
            Input.Reason = "登记真实发放";
        }
        await ReloadPersonLinesAsync(cancellationToken, details);
    }

    private async Task ReloadPersonLinesAsync(CancellationToken cancellationToken, PayrollDisbursementBatchDetailsDto? details = null)
    {
        if (details is null && Id.HasValue) details = await payrollService.GetDisbursementBatchAsync(Id.Value, cancellationToken);
        var existing = details?.Lines
            .Where(item => item.RecipientType is PayrollRecipientType.Employee or PayrollRecipientType.CrewWorker)
            .ToDictionary(item => (
                item.RecipientType,
                item.EmployeeId ?? item.ConstructionWorkerId!.Value,
                item.RecipientType == PayrollRecipientType.CrewWorker ? item.CrewBusinessPartnerId : null)) ?? [];
        var employees = await db.Employees.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.EmployeeNumber).ToListAsync(cancellationToken);
        var employeeLines = employees.Select(item => MakeLine(existing, PayrollRecipientType.Employee, item.Id, item.EmployeeNumber + " · " + item.Name + " · " + item.EmployeeType.ToChinese(), null)).ToList();
        foreach (var line in existing.Values.Where(item => item.RecipientType == PayrollRecipientType.Employee && employeeLines.All(candidate => candidate.PersonId != item.EmployeeId)))
        {
            employeeLines.Add(MakeLine(existing, PayrollRecipientType.Employee, line.EmployeeId!.Value, line.RecipientNameSnapshot, null));
        }
        Input.EmployeeLines = employeeLines;
        var memberships = await db.ConstructionCrewMemberships.AsNoTracking().Where(item => !item.EndDate.HasValue && item.Worker.IsActive && item.CrewBusinessPartner.IsActive && item.CrewBusinessPartner.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew))
            .Include(item => item.Worker).Include(item => item.CrewBusinessPartner).OrderBy(item => item.CrewBusinessPartner.Name).ThenBy(item => item.Worker.Name).ToListAsync(cancellationToken);
        var crewLines = memberships.Select(item => MakeLine(existing, PayrollRecipientType.CrewWorker, item.Worker.Id, item.Worker.Name, item.CrewBusinessPartnerId, item.CrewBusinessPartner.Name)).ToList();
        foreach (var line in existing.Values.Where(item => item.RecipientType == PayrollRecipientType.CrewWorker && crewLines.All(candidate =>
                     candidate.PersonId != item.ConstructionWorkerId || candidate.CrewBusinessPartnerId != item.CrewBusinessPartnerId)))
        {
            crewLines.Add(MakeLine(existing, PayrollRecipientType.CrewWorker, line.ConstructionWorkerId!.Value, line.RecipientNameSnapshot, line.CrewBusinessPartnerId, line.CrewNameSnapshot));
        }
        Input.CrewLines = crewLines;
    }

    private static PersonLineInput MakeLine(Dictionary<(PayrollRecipientType, Guid, Guid?), PayrollDisbursementLineDto> existing, PayrollRecipientType type, Guid personId, string label, Guid? crewId, string? crewName = null)
    {
        existing.TryGetValue((type, personId, crewId), out var line);
        return new PersonLineInput
        {
            PaymentId = line?.Id,
            PersonId = personId,
            CrewBusinessPartnerId = crewId,
            CrewName = crewName,
            Label = label,
            Selected = line is not null,
            Amount = line?.Amount ?? 0m,
            Notes = line?.Notes,
            PaymentCategory = line?.PaymentCategory ?? PayrollPaymentCategory.Wage,
            WageCategory = line?.WageCategory ?? (crewId.HasValue ? EmployeeWageCategory.MigrantWorkerWage : EmployeeWageCategory.SocialSecurityWage),
            LaborBusinessPartnerId = line?.LaborBusinessPartnerId,
            ProjectId = line?.ProjectId
        };
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        Projects = await db.Projects.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ProjectNumber).Select(item => new SelectOption(item.Id, item.ProjectNumber + " · " + item.Name)).ToListAsync(cancellationToken);
        Companies = await db.LegalEntities.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code).Select(item => new SelectOption(item.Id, item.ShortName)).ToListAsync(cancellationToken);
        Accounts = await db.FinancialAccounts.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.AccountName).Select(item => new AccountOption(item.Id, item.LegalEntityId, item.AccountName, item.AccountType, item.OwnerName, item.OwnerEmployeeId)).ToListAsync(cancellationToken);
        LaborPartners = await db.BusinessPartners.AsNoTracking()
            .Where(item => item.IsActive && item.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew))
            .OrderBy(item => item.Name)
            .Select(item => new SelectOption(item.Id, item.Name))
            .ToListAsync(cancellationToken);
    }

    private static bool IsLocalReturnUrl(string? value) => !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal);

    public sealed record SelectOption(Guid Id, string Label);
    public sealed record AccountOption(Guid Id, Guid LegalEntityId, string Label, FinancialAccountType Type, string? OwnerName, Guid? OwnerEmployeeId);
    public sealed class BatchInput
    {
        public string BatchNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateOnly? PaymentDate { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? PersonalAdvanceAccountId { get; set; }
        public decimal ActualAmount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
        public string? VoucherNumber { get; set; }
        public PayrollBatchStatus Status { get; set; }
        public string? Notes { get; set; }
        public PayrollDisbursementType DisbursementType { get; set; } = PayrollDisbursementType.Wage;
        public PayrollFundingSource FundingSource { get; set; } = PayrollFundingSource.CompanyAccount;
        public Guid? RepaysPersonalAdvanceAccountId { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<PersonLineInput> EmployeeLines { get; set; } = [];
        public List<PersonLineInput> CrewLines { get; set; } = [];
    }

    public sealed class PersonLineInput
    {
        public Guid? PaymentId { get; set; }
        public Guid PersonId { get; set; }
        public Guid? CrewBusinessPartnerId { get; set; }
        public string? CrewName { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool Selected { get; set; }
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
        public PayrollPaymentCategory PaymentCategory { get; set; } = PayrollPaymentCategory.Wage;
        public EmployeeWageCategory? WageCategory { get; set; } = EmployeeWageCategory.SocialSecurityWage;
        public Guid? LaborBusinessPartnerId { get; set; }
        public Guid? ProjectId { get; set; }
    }
}
