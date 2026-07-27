using System.Security.Claims;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Web.Presentation;
using EngineeringManager.Web.Workbenches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Payroll;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IPayrollService payrollService, ApplicationDbContext db) : PageModel
{
    public const string PersonalDisbursementScope = "personal";

    public PayrollDisbursementOverviewDto Overview { get; private set; } = new(0m, 0m, 0m, 0m, []);
    public IReadOnlyList<PayrollDisbursementBatchListItemDto> Batches { get; private set; } = [];
    public IReadOnlyList<DataWorkbenchFilterOption> DisbursementScopeOptions { get; private set; } = [];
    public string DisbursementScopeLabel { get; private set; } = "全部发放主体";
    public IReadOnlyDictionary<Guid, string> ProjectLabels { get; private set; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, string> CompanyLabels { get; private set; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, string> AccountLabels { get; private set; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, PayrollRecipientBreakdownViewModel> RecipientBreakdowns { get; private set; } = new Dictionary<Guid, PayrollRecipientBreakdownViewModel>();
    public PayrollEditorViewModel? EditorViewModel { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public PayrollBatchStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? DisbursementScope { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? LineId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty(SupportsGet = true)] public string? Dialog { get; set; }
    [BindProperty] public PayrollEditorInput Input { get; set; } = new();

    public bool CanViewSensitive => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator);

    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator)
        || User.IsInRole(SystemRoles.Finance);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadWorkspaceAsync(cancellationToken);
        if (string.Equals(Dialog, "editor", StringComparison.OrdinalIgnoreCase) && CanManage)
        {
            await LoadEditorAsync(cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();

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
            var crewAllocations = lines.Where(item => item.CrewBusinessPartnerId.HasValue)
                .Select(item => item.CrewBusinessPartnerId!.Value)
                .Distinct()
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
            return RedirectToPage(new { id = saved.Batch.Id, dialog = "details", search = Search, status = Status, disbursementScope = DisbursementScope });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Dialog = "editor";
            await LoadWorkspaceAsync(cancellationToken);
            await LoadEditorAsync(cancellationToken, preservePostedValues: true);
            return Page();
        }
    }

    public async Task LoadEditorAsync(CancellationToken cancellationToken, bool preservePostedValues = false)
    {
        var postedEmployeeLines = preservePostedValues ? Input.EmployeeLines.ToArray() : [];
        var postedCrewLines = preservePostedValues ? Input.CrewLines.ToArray() : [];
        PayrollDisbursementBatchDetailsDto? details = null;
        if (!preservePostedValues)
        {
            if (Id.HasValue)
            {
                details = await payrollService.GetDisbursementBatchAsync(Id.Value, cancellationToken)
                    ?? throw new InvalidOperationException("工资批次不存在。");
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
        }
        else if (Id.HasValue)
        {
            details = await payrollService.GetDisbursementBatchAsync(Id.Value, cancellationToken);
        }

        var options = await LoadOptionsAsync(cancellationToken);
        await ReloadPersonLinesAsync(details, postedEmployeeLines, postedCrewLines, cancellationToken);
        EditorViewModel = new PayrollEditorViewModel(Id, LineId, ReturnUrl, Input, options.Projects, options.Companies, options.Accounts, options.LaborPartners);
    }

    private async Task LoadWorkspaceAsync(CancellationToken cancellationToken)
    {
        var sourceOverview = string.IsNullOrWhiteSpace(Search)
            ? await payrollService.GetDisbursementOverviewAsync(cancellationToken)
            : await payrollService.SearchDisbursementOverviewAsync(Search, CanViewSensitive, cancellationToken);

        DisbursementScopeOptions = (await db.LegalEntities.AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Code)
                .Select(item => new { item.Id, item.ShortName })
                .ToListAsync(cancellationToken))
            .Select(item => new DataWorkbenchFilterOption(CompanyScope(item.Id), item.ShortName))
            .Append(new DataWorkbenchFilterOption(PersonalDisbursementScope, "私人转账（个人垫付）"))
            .ToArray();

        var filteredBatches = FilterByDisbursementScope(sourceOverview.Batches);
        if (Status.HasValue)
        {
            filteredBatches = filteredBatches.Where(item => item.Batch.Status == Status.Value).ToArray();
        }

        Batches = filteredBatches;
        Overview = BuildOverview(Batches);

        var projectIds = Batches.Select(item => item.Batch.ProjectId).OfType<Guid>().Distinct().ToArray();
        ProjectLabels = await db.Projects.AsNoTracking().Where(item => projectIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.ProjectNumber + " · " + item.Name, cancellationToken);
        var companyIds = Batches.Select(item => item.Batch.LegalEntityId).OfType<Guid>().Distinct().ToArray();
        CompanyLabels = await db.LegalEntities.AsNoTracking().Where(item => companyIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.ShortName, cancellationToken);
        var accountIds = Batches.Select(item => item.Batch.AccountId).OfType<Guid>().Distinct().ToArray();
        AccountLabels = await db.FinancialAccounts.AsNoTracking().Where(item => accountIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.AccountName, cancellationToken);

        await LoadRecipientBreakdownsAsync(cancellationToken);
    }

    private async Task LoadRecipientBreakdownsAsync(CancellationToken cancellationToken)
    {
        var batchIds = Batches.Select(item => item.Batch.Id).ToArray();
        if (batchIds.Length == 0)
        {
            RecipientBreakdowns = new Dictionary<Guid, PayrollRecipientBreakdownViewModel>();
            return;
        }

        var recipients = await db.PayrollPayments.AsNoTracking()
            .Where(item => batchIds.Contains(item.PayrollBatchId))
            .Select(item => new
            {
                item.PayrollBatchId,
                item.RecipientType,
                Name = item.RecipientNameSnapshot
                    ?? (item.Employee != null ? item.Employee.Name : null)
                    ?? (item.ConstructionWorker != null ? item.ConstructionWorker.Name : null)
                    ?? item.PayeeName,
                GroupName = item.RecipientType == PayrollRecipientType.CrewWorker
                    ? item.CrewNameSnapshot ?? (item.CrewBusinessPartner != null ? item.CrewBusinessPartner.Name : null)
                    : null,
                item.Amount
            })
            .ToListAsync(cancellationToken);

        RecipientBreakdowns = recipients
            .GroupBy(item => item.PayrollBatchId)
            .ToDictionary(
                group => group.Key,
                group => new PayrollRecipientBreakdownViewModel(
                    group.Where(item => item.RecipientType == PayrollRecipientType.Employee)
                        .OrderBy(item => item.Name)
                        .Select(item => new PayrollRecipientItemViewModel(item.Name, null, item.Amount))
                        .ToArray(),
                    group.Where(item => item.RecipientType == PayrollRecipientType.CrewWorker)
                        .OrderBy(item => item.GroupName)
                        .ThenBy(item => item.Name)
                        .Select(item => new PayrollRecipientItemViewModel(item.Name, item.GroupName, item.Amount))
                        .ToArray()));
    }

    private IReadOnlyList<PayrollDisbursementBatchListItemDto> FilterByDisbursementScope(
        IReadOnlyList<PayrollDisbursementBatchListItemDto> batches)
    {
        if (string.Equals(DisbursementScope, PersonalDisbursementScope, StringComparison.OrdinalIgnoreCase))
        {
            DisbursementScope = PersonalDisbursementScope;
            DisbursementScopeLabel = "私人转账（个人垫付）";
            return batches.Where(item => item.Batch.FundingSource == PayrollFundingSource.PersonalAdvance).ToArray();
        }

        const string companyPrefix = "company:";
        if (DisbursementScope?.StartsWith(companyPrefix, StringComparison.OrdinalIgnoreCase) == true
            && Guid.TryParse(DisbursementScope[companyPrefix.Length..], out var companyId))
        {
            var option = DisbursementScopeOptions.FirstOrDefault(item => item.Value == CompanyScope(companyId));
            if (option is not null)
            {
                DisbursementScope = option.Value;
                DisbursementScopeLabel = option.Label;
                return batches.Where(item =>
                    item.Batch.FundingSource == PayrollFundingSource.CompanyAccount
                    && item.Batch.LegalEntityId == companyId).ToArray();
            }
        }

        DisbursementScope = null;
        DisbursementScopeLabel = "全部发放主体";
        return batches;
    }

    private static PayrollDisbursementOverviewDto BuildOverview(IReadOnlyList<PayrollDisbursementBatchListItemDto> batches) =>
        new(
            batches.Sum(item => item.Batch.ActualAmount),
            batches.Sum(item => item.Summary.EmployeeAmount),
            batches.Sum(item => item.Summary.CrewAmount),
            batches.Sum(item => item.Summary.Difference),
            batches);

    private static string CompanyScope(Guid companyId) => $"company:{companyId}";

    private async Task ReloadPersonLinesAsync(
        PayrollDisbursementBatchDetailsDto? details,
        IReadOnlyList<PayrollPersonLineInput> postedEmployeeLines,
        IReadOnlyList<PayrollPersonLineInput> postedCrewLines,
        CancellationToken cancellationToken)
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
        Input.EmployeeLines = MergePostedLines(employeeLines, postedEmployeeLines);

        var memberships = await db.ConstructionCrewMemberships.AsNoTracking()
            .Where(item => !item.EndDate.HasValue && item.Worker.IsActive && item.CrewBusinessPartner.IsActive && item.CrewBusinessPartner.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew))
            .Include(item => item.Worker)
            .Include(item => item.CrewBusinessPartner)
            .OrderBy(item => item.CrewBusinessPartner.Name)
            .ThenBy(item => item.Worker.Name)
            .ToListAsync(cancellationToken);
        var crewLines = memberships.Select(item => MakeLine(existing, PayrollRecipientType.CrewWorker, item.Worker.Id, item.Worker.Name, item.CrewBusinessPartnerId, item.CrewBusinessPartner.Name)).ToList();
        foreach (var line in existing.Values.Where(item => item.RecipientType == PayrollRecipientType.CrewWorker && crewLines.All(candidate =>
                     candidate.PersonId != item.ConstructionWorkerId || candidate.CrewBusinessPartnerId != item.CrewBusinessPartnerId)))
        {
            crewLines.Add(MakeLine(existing, PayrollRecipientType.CrewWorker, line.ConstructionWorkerId!.Value, line.RecipientNameSnapshot, line.CrewBusinessPartnerId, line.CrewNameSnapshot));
        }
        Input.CrewLines = MergePostedLines(crewLines, postedCrewLines);
    }

    private static List<PayrollPersonLineInput> MergePostedLines(
        IReadOnlyList<PayrollPersonLineInput> available,
        IReadOnlyList<PayrollPersonLineInput> posted)
    {
        if (posted.Count == 0) return available.ToList();
        var postedMap = posted.ToDictionary(item => (item.PersonId, item.CrewBusinessPartnerId));
        return available.Select(item => postedMap.GetValueOrDefault((item.PersonId, item.CrewBusinessPartnerId)) ?? item).ToList();
    }

    private static PayrollPersonLineInput MakeLine(
        Dictionary<(PayrollRecipientType, Guid, Guid?), PayrollDisbursementLineDto> existing,
        PayrollRecipientType type,
        Guid personId,
        string label,
        Guid? crewId,
        string? crewName = null)
    {
        existing.TryGetValue((type, personId, crewId), out var line);
        return new PayrollPersonLineInput
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

    private async Task<EditorOptions> LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var projects = await db.Projects.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ProjectNumber)
            .Select(item => new PayrollSelectOption(item.Id, item.ProjectNumber + " · " + item.Name)).ToListAsync(cancellationToken);
        var companies = await db.LegalEntities.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code)
            .Select(item => new PayrollSelectOption(item.Id, item.ShortName)).ToListAsync(cancellationToken);
        var accounts = await db.FinancialAccounts.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.AccountName)
            .Select(item => new PayrollAccountOption(item.Id, item.LegalEntityId, item.AccountName, item.AccountType, item.OwnerName, item.OwnerEmployeeId)).ToListAsync(cancellationToken);
        var laborPartners = await db.BusinessPartners.AsNoTracking()
            .Where(item => item.IsActive && item.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew))
            .OrderBy(item => item.Name)
            .Select(item => new PayrollSelectOption(item.Id, item.Name))
            .ToListAsync(cancellationToken);
        return new EditorOptions(projects, companies, accounts, laborPartners);
    }

    private static bool IsLocalReturnUrl(string? value) => !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal);

    private sealed record EditorOptions(
        IReadOnlyList<PayrollSelectOption> Projects,
        IReadOnlyList<PayrollSelectOption> Companies,
        IReadOnlyList<PayrollAccountOption> Accounts,
        IReadOnlyList<PayrollSelectOption> LaborPartners);
}
