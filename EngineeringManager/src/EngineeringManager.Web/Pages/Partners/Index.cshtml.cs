using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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

namespace EngineeringManager.Web.Pages.Partners;

[Authorize]
public sealed class IndexModel(
    IBusinessPartnerService partnerService,
    ICentralLedgerQueryService ledgerQueryService,
    ApplicationDbContext db,
    IOrganizationSummaryService? organizationSummaryService = null) : PageModel
{
    public const string CustomerScope = "customers";

    public IReadOnlyList<BusinessPartnerDto> AllPartners { get; private set; } = [];
    public IReadOnlyList<BusinessPartnerDto> Partners { get; private set; } = [];
    public IReadOnlyList<PartnerRoleSummary> RoleSummaries { get; private set; } = [];
    public IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto> PartnerFinancialSummaries { get; private set; }
        = new Dictionary<Guid, PartnerLedgerSummaryDto>();
    public IReadOnlyDictionary<Guid, OrganizationSummaryDto> OrganizationSummaries { get; private set; }
        = new Dictionary<Guid, OrganizationSummaryDto>();
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.ProjectManager);
    public bool CanManageFinance => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
    public bool CanViewFinance => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance) || User.IsInRole(SystemRoles.QueryOnly);
    public string? ActiveDialog { get; private set; }
    public string NextPartnerNumber { get; private set; } = "HZ0001";
    public bool IsCustomerScope => string.Equals(Scope, CustomerScope, StringComparison.OrdinalIgnoreCase);
    public string EntityLabel => IsCustomerScope ? "甲方/总包" : "合作单位";
    public BusinessPartnerRoleType DefaultRole => IsCustomerScope
        ? BusinessPartnerRoleType.CustomerOrGeneralContractor
        : BusinessPartnerRoleType.ConstructionCrew;
    public IReadOnlyList<BusinessPartnerRoleType> AvailableRoles => IsCustomerScope
        ? [BusinessPartnerRoleType.CustomerOrGeneralContractor]
        : Enum.GetValues<BusinessPartnerRoleType>()
            .Where(role => role != BusinessPartnerRoleType.CustomerOrGeneralContractor)
            .ToArray();

    [BindProperty(SupportsGet = true)] public string? Scope { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public BusinessPartnerRoleType? Role { get; set; }
    [BindProperty(SupportsGet = true)] public bool? IsActive { get; set; }
    [BindProperty] public PartnerEditorInput Editor { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        NormalizeRoleFilter();
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        NormalizeRoleFilter();
        if (IsCustomerScope)
        {
            Editor.RoleType = BusinessPartnerRoleType.CustomerOrGeneralContractor;
        }
        else if (Editor.RoleType == BusinessPartnerRoleType.CustomerOrGeneralContractor)
        {
            ModelState.AddModelError(string.Empty, "甲方/总包请在独立页面中维护。");
        }
        await EnsureEditorTargetMatchesScopeAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            ActiveDialog = "editor";
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            var role = new PartnerRoleRequest(Editor.RoleType, Editor.TradeCategory, null, null);
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
                TempData["SuccessMessage"] = $"{EntityLabel}已更新。";
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
                TempData["SuccessMessage"] = $"{EntityLabel}已新增。";
            }

            return RedirectToPage(new
            {
                Scope = IsCustomerScope ? CustomerScope : null,
                Search,
                Role = IsCustomerScope ? null : Role,
                IsActive
            });
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
        var allPartners = await partnerService.ListForManagementAsync(null, null, cancellationToken);
        NextPartnerNumber = ShortBusinessNumber.Next(allPartners.Select(item => item.PartnerNumber), "HZ");
        AllPartners = ApplyScope(allPartners).ToArray();
        var filtered = await partnerService.ListForManagementAsync(
            Search,
            IsCustomerScope ? BusinessPartnerRoleType.CustomerOrGeneralContractor : Role,
            cancellationToken);
        var scoped = ApplyScope(filtered);
        Partners = (IsActive.HasValue ? scoped.Where(item => item.IsActive == IsActive.Value) : scoped).ToArray();
        RoleSummaries = AvailableRoles
            .Select(role => new PartnerRoleSummary(role, AllPartners.Count(item => item.Roles.Any(value => value.RoleType == role))))
            .ToArray();
        if (organizationSummaryService is not null)
        {
            var summaries = new Dictionary<Guid, OrganizationSummaryDto>();
            var asOf = DateOnly.FromDateTime(DateTime.Today);
            foreach (var partner in Partners)
            {
                summaries[partner.Id] = await organizationSummaryService.GetAsync(
                    new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, partner.Id, asOf),
                    cancellationToken);
            }
            OrganizationSummaries = summaries;
        }
        if (CanViewFinance && Partners.Count > 0)
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, cancellationToken);
            PartnerFinancialSummaries = await ledgerQueryService.GetPartnerSummariesAsync(
                actor,
                Partners.Select(item => item.Id).ToArray(),
                cancellationToken);
        }
    }

    private IEnumerable<BusinessPartnerDto> ApplyScope(IEnumerable<BusinessPartnerDto> partners) =>
        IsCustomerScope
            ? partners.Where(item => HasRole(item, BusinessPartnerRoleType.CustomerOrGeneralContractor))
            : partners.Where(item => !HasRole(item, BusinessPartnerRoleType.CustomerOrGeneralContractor));

    private void NormalizeRoleFilter()
    {
        if (IsCustomerScope || Role == BusinessPartnerRoleType.CustomerOrGeneralContractor)
        {
            Role = null;
        }
    }

    private async Task EnsureEditorTargetMatchesScopeAsync(CancellationToken cancellationToken)
    {
        if (!Editor.Id.HasValue) return;

        var partners = await partnerService.ListForManagementAsync(null, null, cancellationToken);
        var existing = partners.SingleOrDefault(item => item.Id == Editor.Id.Value);
        if (existing is null)
        {
            ModelState.AddModelError(string.Empty, "单位不存在或已删除，请刷新后重试。");
            return;
        }

        var belongsToCustomerScope = HasRole(existing, BusinessPartnerRoleType.CustomerOrGeneralContractor);
        if (belongsToCustomerScope != IsCustomerScope)
        {
            ModelState.AddModelError(string.Empty, "当前单位不属于此维护页面，请刷新后重试。");
        }
    }

    private static bool HasRole(BusinessPartnerDto partner, BusinessPartnerRoleType role) =>
        partner.Roles.Any(item => item.RoleType == role);

    public sealed record PartnerRoleSummary(BusinessPartnerRoleType Role, int Count);

    public sealed class PartnerEditorInput
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "请填写单位编号。"), StringLength(50)] public string PartnerNumber { get; set; } = string.Empty;
        [Required(ErrorMessage = "请填写单位全称。"), StringLength(200)] public string Name { get; set; } = string.Empty;
        [Required(ErrorMessage = "请填写单位简称。"), StringLength(100)] public string ShortName { get; set; } = string.Empty;
        [StringLength(50)] public string? UnifiedSocialCreditCode { get; set; }
        public BusinessPartnerRoleType RoleType { get; set; } = BusinessPartnerRoleType.ConstructionCrew;
        [StringLength(100)] public string? TradeCategory { get; set; }
        [StringLength(100)] public string? ContactName { get; set; }
        [StringLength(50)] public string? ContactPhone { get; set; }
        [StringLength(500)] public string? ContactNotes { get; set; }
        [StringLength(1000)] public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ConcurrencyStamp { get; set; }
        [Required(ErrorMessage = "请填写修改原因。"), StringLength(500)] public string Reason { get; set; } = "维护合作单位资料";
    }
}
