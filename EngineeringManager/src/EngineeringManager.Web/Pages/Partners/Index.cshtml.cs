using System.Security.Claims;
using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Partners;

[Authorize]
public sealed class IndexModel(IBusinessPartnerService partnerService) : PageModel
{
    public IReadOnlyList<BusinessPartnerDto> Partners { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.ProjectManager);
    public bool QuickEditOpen { get; private set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty] public QuickEditInput QuickEdit { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Partners = await partnerService.ListAsync(Search, null, cancellationToken);
    }

    public async Task<IActionResult> OnPostQuickEditAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        try
        {
            var existing = await partnerService.GetAsync(QuickEdit.Id, cancellationToken);
            var existingContactNotes = existing?.Contacts.FirstOrDefault(item => item.IsPrimary)?.Notes;
            await partnerService.UpdateAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                new UpdateBusinessPartnerRequest(
                    QuickEdit.Id,
                    QuickEdit.PartnerNumber,
                    QuickEdit.Name,
                    QuickEdit.ShortName,
                    QuickEdit.UnifiedSocialCreditCode,
                    QuickEdit.Notes,
                    new PartnerRoleRequest(QuickEdit.RoleType, QuickEdit.TradeCategory, null, null),
                    string.IsNullOrWhiteSpace(QuickEdit.ContactName) ? null : new PartnerContactRequest(QuickEdit.ContactName, QuickEdit.ContactPhone, null, null, true, existingContactNotes),
                    QuickEdit.IsActive,
                    QuickEdit.ConcurrencyStamp,
                    QuickEdit.Reason),
                cancellationToken);
            return RedirectToPage(new { search = Search });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            QuickEditOpen = true;
            Partners = await partnerService.ListAsync(Search, null, cancellationToken);
            return Page();
        }
    }

    public sealed class QuickEditInput
    {
        public Guid Id { get; set; }
        public string PartnerNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public BusinessPartnerRoleType RoleType { get; set; } = BusinessPartnerRoleType.ConstructionCrew;
        public string? TradeCategory { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? UnifiedSocialCreditCode { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ConcurrencyStamp { get; set; }
        public string Reason { get; set; } = "快捷编辑合作单位";
    }
}
