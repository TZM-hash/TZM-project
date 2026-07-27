using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Web.Pages.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Partners;

[Authorize]
public sealed class IndexModel(
    IBusinessPartnerService partnerService,
    ICentralLedgerQueryService ledgerQueryService,
    ApplicationDbContext db) : PageModel
{
    public IReadOnlyList<BusinessPartnerDto> AllPartners { get; private set; } = [];
    public IReadOnlyList<BusinessPartnerDto> Partners { get; private set; } = [];
    public IReadOnlyList<PartnerRoleSummary> RoleSummaries { get; private set; } = [];
    public IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto> PartnerFinancialSummaries { get; private set; }
        = new Dictionary<Guid, PartnerLedgerSummaryDto>();
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.ProjectManager);
    public bool CanManageFinance => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
    public bool CanViewFinance => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance) || User.IsInRole(SystemRoles.QueryOnly);
    public string? ActiveDialog { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public BusinessPartnerRoleType? Role { get; set; }
    [BindProperty(SupportsGet = true)] public bool? IsActive { get; set; }
    [BindProperty] public PartnerEditorInput Editor { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

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
                TempData["SuccessMessage"] = "合作单位已更新。";
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
                TempData["SuccessMessage"] = "合作单位已新增。";
            }

            return RedirectToPage(new { Search, Role, IsActive });
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
        AllPartners = await partnerService.ListForManagementAsync(null, null, cancellationToken);
        var filtered = await partnerService.ListForManagementAsync(Search, Role, cancellationToken);
        Partners = (IsActive.HasValue ? filtered.Where(item => item.IsActive == IsActive.Value) : filtered).ToArray();
        RoleSummaries = Enum.GetValues<BusinessPartnerRoleType>()
            .Select(role => new PartnerRoleSummary(role, AllPartners.Count(item => item.Roles.Any(value => value.RoleType == role))))
            .ToArray();
        if (CanViewFinance && Partners.Count > 0)
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, cancellationToken);
            PartnerFinancialSummaries = await ledgerQueryService.GetPartnerSummariesAsync(
                actor,
                Partners.Select(item => item.Id).ToArray(),
                cancellationToken);
        }
    }

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
