using System.Security.Claims;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
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
            var lines = Input.EmployeeLines.Where(item => item.Selected && item.Amount > 0m)
                .Select(item => new PayrollDisbursementLineRequest(item.PaymentId, PayrollRecipientType.Employee, item.PersonId, null, null, null, item.Amount, item.Notes))
                .Concat(Input.CrewLines.Where(item => item.Selected && item.Amount > 0m)
                    .Select(item => new PayrollDisbursementLineRequest(item.PaymentId, PayrollRecipientType.CrewWorker, null, item.PersonId, null, item.CrewBusinessPartnerId, item.Amount, item.Notes)))
                .Concat(Input.TemporaryLines.Where(item => item.Selected && item.Amount > 0m)
                    .Select(item => new PayrollDisbursementLineRequest(item.PaymentId, PayrollRecipientType.TemporaryWorker, null, null, item.PersonId, null, item.Amount, item.Notes)))
                .ToArray();
            var crewAllocations = lines.Where(item => item.CrewBusinessPartnerId.HasValue).Select(item => item.CrewBusinessPartnerId!.Value).Distinct()
                .Select(crewId => new PayrollCrewAllocationRequest(crewId, null, null, "工程款待关联"))
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
                    Input.AccountId,
                    Input.ActualAmount,
                    Input.PaymentMethod,
                    Input.VoucherNumber,
                    Input.Status,
                    Input.Notes,
                    Input.ConcurrencyStamp,
                    Input.Reason,
                    lines,
                    crewAllocations),
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
            Input.AccountId = details.Batch.AccountId;
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
        var existing = details?.Lines.ToDictionary(item => (item.RecipientType, item.EmployeeId ?? item.ConstructionWorkerId ?? item.TemporaryWorkerId!.Value)) ?? [];
        var employees = await db.Employees.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.EmployeeNumber).ToListAsync(cancellationToken);
        Input.EmployeeLines = employees.Select(item => MakeLine(existing, PayrollRecipientType.Employee, item.Id, item.EmployeeNumber + " · " + item.Name, null)).ToList();
        var memberships = await db.ConstructionCrewMemberships.AsNoTracking().Where(item => !item.EndDate.HasValue && item.Worker.IsActive && item.CrewBusinessPartner.IsActive && item.CrewBusinessPartner.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew))
            .Include(item => item.Worker).Include(item => item.CrewBusinessPartner).OrderBy(item => item.CrewBusinessPartner.Name).ThenBy(item => item.Worker.Name).ToListAsync(cancellationToken);
        Input.CrewLines = memberships.Select(item => MakeLine(existing, PayrollRecipientType.CrewWorker, item.Worker.Id, item.Worker.Name, item.CrewBusinessPartnerId, item.CrewBusinessPartner.Name)).ToList();
        var temporaryWorkers = await db.TemporaryWorkers.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        Input.TemporaryLines = temporaryWorkers.Select(item => MakeLine(existing, PayrollRecipientType.TemporaryWorker, item.Id, item.Name, null)).ToList();
    }

    private static PersonLineInput MakeLine(Dictionary<(PayrollRecipientType, Guid), PayrollDisbursementLineDto> existing, PayrollRecipientType type, Guid personId, string label, Guid? crewId, string? crewName = null)
    {
        existing.TryGetValue((type, personId), out var line);
        return new PersonLineInput { PaymentId = line?.Id, PersonId = personId, CrewBusinessPartnerId = crewId, CrewName = crewName, Label = label, Selected = line is not null, Amount = line?.Amount ?? 0m, Notes = line?.Notes };
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        Projects = await db.Projects.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ProjectNumber).Select(item => new SelectOption(item.Id, item.ProjectNumber + " · " + item.Name)).ToListAsync(cancellationToken);
        Companies = await db.LegalEntities.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code).Select(item => new SelectOption(item.Id, item.ShortName)).ToListAsync(cancellationToken);
        Accounts = await db.FinancialAccounts.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.AccountName).Select(item => new AccountOption(item.Id, item.LegalEntityId, item.AccountName)).ToListAsync(cancellationToken);
    }

    private static bool IsLocalReturnUrl(string? value) => !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal);

    public sealed record SelectOption(Guid Id, string Label);
    public sealed record AccountOption(Guid Id, Guid LegalEntityId, string Label);
    public sealed class BatchInput
    {
        public string BatchNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateOnly? PaymentDate { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? LegalEntityId { get; set; }
        public Guid? AccountId { get; set; }
        public decimal ActualAmount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
        public string? VoucherNumber { get; set; }
        public PayrollBatchStatus Status { get; set; }
        public string? Notes { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<PersonLineInput> EmployeeLines { get; set; } = [];
        public List<PersonLineInput> CrewLines { get; set; } = [];
        public List<PersonLineInput> TemporaryLines { get; set; } = [];
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
    }
}
