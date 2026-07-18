using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Partners;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.ProjectManager)]
public sealed class CreateModel(IBusinessPartnerService partnerService) : PageModel
{
    public bool IsEditing => Id.HasValue;
    [BindProperty] public Guid? Id { get; set; }
    [BindProperty] public string PartnerNumber { get; set; } = string.Empty;
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string ShortName { get; set; } = string.Empty;
    [BindProperty] public BusinessPartnerRoleType RoleType { get; set; } = BusinessPartnerRoleType.ConstructionCrew;
    [BindProperty] public string? TradeCategory { get; set; }
    [BindProperty] public string? ContactName { get; set; }
    [BindProperty] public string? ContactPhone { get; set; }
    [BindProperty] public string? ContactNotes { get; set; }
    [BindProperty] public string? UnifiedSocialCreditCode { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public Guid ConcurrencyStamp { get; set; }
    [BindProperty] public string Reason { get; set; } = "维护合作单位资料";

    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? copyFrom, CancellationToken token)
    {
        var sourceId = id ?? copyFrom;
        if (!sourceId.HasValue) return Page();
        var partner = await partnerService.GetAsync(sourceId.Value, token);
        if (partner is null) return NotFound();
        Id = partner.Id; PartnerNumber = partner.PartnerNumber; Name = partner.Name; ShortName = partner.ShortName; UnifiedSocialCreditCode = partner.UnifiedSocialCreditCode; Notes = partner.Notes; IsActive = partner.IsActive; ConcurrencyStamp = partner.ConcurrencyStamp;
        var role = partner.Roles.Count > 0 ? partner.Roles[0] : null; if (role is not null) { RoleType = role.RoleType; TradeCategory = role.TradeCategory; }
        var contact = partner.Contacts.FirstOrDefault(item => item.IsPrimary) ?? (partner.Contacts.Count > 0 ? partner.Contacts[0] : null); if (contact is not null) { ContactName = contact.Name; ContactPhone = contact.Phone; ContactNotes = contact.Notes; }
        if (copyFrom.HasValue) { Id = null; PartnerNumber += "-COPY"; Name += "（复制）"; ShortName += "副本"; UnifiedSocialCreditCode = null; ContactName = null; ContactPhone = null; ConcurrencyStamp = Guid.Empty; Reason = "复制合作单位"; }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (Id.HasValue)
                await partnerService.UpdateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", new UpdateBusinessPartnerRequest(Id.Value, PartnerNumber, Name, ShortName, UnifiedSocialCreditCode, Notes, new PartnerRoleRequest(RoleType, TradeCategory, null, null), string.IsNullOrWhiteSpace(ContactName) ? null : new PartnerContactRequest(ContactName, ContactPhone, null, null, true, ContactNotes), IsActive, ConcurrencyStamp, Reason), cancellationToken);
            else
                await partnerService.CreateAsync(new CreateBusinessPartnerRequest(PartnerNumber, Name, ShortName, UnifiedSocialCreditCode, Notes, [new PartnerRoleRequest(RoleType, TradeCategory, null, null)], string.IsNullOrWhiteSpace(ContactName) ? [] : [new PartnerContactRequest(ContactName, ContactPhone, null, null, true, ContactNotes)]), cancellationToken);
            return RedirectToPage("/Partners/Index");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }
}
