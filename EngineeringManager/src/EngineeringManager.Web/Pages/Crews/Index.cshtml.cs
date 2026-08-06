using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using EngineeringManager.Application.ConstructionCrews;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Organization;
using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Web.Pages.Ledger;
using EngineeringManager.Web.Presentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Crews;

[Authorize]
public sealed class IndexModel(
    IConstructionCrewService crewService,
    IBusinessPartnerService partnerService,
    ICentralLedgerQueryService ledgerQueryService,
    ApplicationDbContext db,
    IOrganizationSummaryService? organizationSummaryService = null) : PageModel
{
    public IReadOnlyList<CrewWorkspaceRow> AllCrews { get; private set; } = [];
    public IReadOnlyList<CrewWorkspaceRow> Crews { get; private set; } = [];
    public IReadOnlyList<CrewTradeSummary> TradeSummaries { get; private set; } = [];
    public IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto> PartnerFinancialSummaries { get; private set; }
        = new Dictionary<Guid, PartnerLedgerSummaryDto>();
    public IReadOnlyDictionary<Guid, OrganizationSummaryDto> OrganizationSummaries { get; private set; }
        = new Dictionary<Guid, OrganizationSummaryDto>();
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator)
        || User.IsInRole(SystemRoles.ProjectManager);
    public bool CanManageFinance => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator)
        || User.IsInRole(SystemRoles.Finance);
    public bool CanViewFinance => CanManageFinance || User.IsInRole(SystemRoles.QueryOnly);
    public bool CanViewSensitive => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public string? ActiveDialog { get; private set; }
    public string NextPartnerNumber { get; private set; } = "HZ0001";

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Trade { get; set; }
    [BindProperty(SupportsGet = true)] public bool? IsActive { get; set; }
    [BindProperty] public CrewEditorInput Editor { get; set; } = new();

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(RedirectToPage(
            "/Partners/Index",
            new
            {
                Category = Partners.IndexModel.CrewCategory,
                Search,
                IsActive
            }));

    public async Task<IActionResult> OnGetRosterAsync(Guid id, CancellationToken cancellationToken)
    {
        var details = await crewService.GetAsync(id, CanViewSensitive, cancellationToken);
        if (details is null) return NotFound();

        return new JsonResult(new
        {
            currentWorkerCount = details.Crew.CurrentWorkerCount,
            historicalWorkerCount = details.Workers.Count,
            projectCount = details.Crew.ProjectCount,
            workers = details.Workers.Select(worker => new
            {
                worker.Name,
                worker.Phone,
                worker.Trade,
                startDate = worker.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                endDate = worker.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                isCurrent = !worker.EndDate.HasValue
            })
        });
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid)
        {
            ActiveDialog = "editor";
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var role = new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, Editor.TradeCategory, null, null);
            var contact = string.IsNullOrWhiteSpace(Editor.ContactName)
                ? null
                : new PartnerContactRequest(Editor.ContactName, Editor.ContactPhone, null, null, true, Editor.ContactNotes);

            if (Editor.Id.HasValue)
            {
                await partnerService.UpdateAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                    new UpdateBusinessPartnerRequest(
                        Editor.Id.Value,
                        Editor.PartnerNumber,
                        Editor.Name,
                        Editor.ShortName,
                        Editor.UnifiedSocialCreditCode,
                        Editor.Notes,
                        role,
                        contact,
                        Editor.IsActive,
                        Editor.ConcurrencyStamp,
                        Editor.Reason),
                    cancellationToken);
                TempData["SuccessMessage"] = "施工班组已更新。";
            }
            else
            {
                await partnerService.CreateAsync(
                    new CreateBusinessPartnerRequest(
                        Editor.PartnerNumber,
                        Editor.Name,
                        Editor.ShortName,
                        Editor.UnifiedSocialCreditCode,
                        Editor.Notes,
                        [role],
                        contact is null ? [] : [contact]),
                    cancellationToken);
                TempData["SuccessMessage"] = "施工班组已新增。";
            }

            return RedirectToPage(new { Search, Trade, IsActive });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            ActiveDialog = "editor";
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var allMetrics = await crewService.ListAsync(true, null, CanViewSensitive, cancellationToken);
        var filteredMetrics = string.IsNullOrWhiteSpace(Search)
            ? allMetrics
            : await crewService.ListAsync(true, Search, CanViewSensitive, cancellationToken);
        var allPartners = await partnerService.ListForManagementAsync(null, null, cancellationToken);
        NextPartnerNumber = ShortBusinessNumber.Next(allPartners.Select(item => item.PartnerNumber), "HZ");
        var partnerMap = allPartners
            .Where(item => item.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew))
            .ToDictionary(item => item.Id);

        AllCrews = Merge(allMetrics, partnerMap);
        Crews = Merge(filteredMetrics, partnerMap)
            .Where(item => string.IsNullOrWhiteSpace(Trade) || item.TradeCategory == Trade)
            .Where(item => !IsActive.HasValue || item.Metrics.IsActive == IsActive.Value)
            .ToArray();
        TradeSummaries = AllCrews
            .GroupBy(item => item.TradeCategory, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CrewTradeSummary(group.Key, group.Count()))
            .ToArray();
        if (organizationSummaryService is not null)
        {
            var asOf = DateOnly.FromDateTime(DateTime.Today);
            OrganizationSummaries = (await organizationSummaryService.GetManyAsync(
                    Crews.Select(item => new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, item.Partner.Id, asOf)).ToArray(),
                    cancellationToken))
                .ToDictionary(item => item.Query.Id);
        }
        if (CanViewFinance && Crews.Count > 0)
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, cancellationToken);
            PartnerFinancialSummaries = await ledgerQueryService.GetPartnerSummariesAsync(
                actor,
                Crews.Select(item => item.Partner.Id).ToArray(),
                cancellationToken);
        }
    }

    private static CrewWorkspaceRow[] Merge(
        IEnumerable<ConstructionCrewListItemDto> metrics,
        Dictionary<Guid, BusinessPartnerDto> partnerMap) =>
        metrics
            .Where(item => partnerMap.ContainsKey(item.Id))
            .Select(item => new CrewWorkspaceRow(partnerMap[item.Id], item))
            .ToArray();

    public sealed record CrewTradeSummary(string Trade, int Count);

    public sealed record CrewWorkspaceRow(BusinessPartnerDto Partner, ConstructionCrewListItemDto Metrics)
    {
        public string TradeCategory => Metrics.TradeCategory ?? "未填写专业";
        public PartnerContactDto? PrimaryContact =>
            Partner.Contacts.FirstOrDefault(item => item.IsPrimary)
            ?? (Partner.Contacts.Count > 0 ? Partner.Contacts[0] : null);
    }

    public sealed class CrewEditorInput
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "请填写班组编号。"), StringLength(50)] public string PartnerNumber { get; set; } = string.Empty;
        [Required(ErrorMessage = "请填写班组全称。"), StringLength(200)] public string Name { get; set; } = string.Empty;
        [Required(ErrorMessage = "请填写班组简称。"), StringLength(100)] public string ShortName { get; set; } = string.Empty;
        [StringLength(50)] public string? UnifiedSocialCreditCode { get; set; }
        [StringLength(100)] public string? TradeCategory { get; set; }
        [StringLength(100)] public string? ContactName { get; set; }
        [StringLength(50)] public string? ContactPhone { get; set; }
        [StringLength(500)] public string? ContactNotes { get; set; }
        [StringLength(1000)] public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ConcurrencyStamp { get; set; }
        [Required(ErrorMessage = "请填写修改原因。"), StringLength(500)] public string Reason { get; set; } = "维护施工班组资料";
    }
}
