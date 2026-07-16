using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Partners;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.ProjectManager)]
public sealed class CreateModel(IBusinessPartnerService partnerService) : PageModel
{
    [BindProperty] public string PartnerNumber { get; set; } = string.Empty;
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string ShortName { get; set; } = string.Empty;
    [BindProperty] public BusinessPartnerRoleType RoleType { get; set; } = BusinessPartnerRoleType.ConstructionCrew;
    [BindProperty] public string? TradeCategory { get; set; }
    [BindProperty] public string? ContactName { get; set; }
    [BindProperty] public string? ContactPhone { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await partnerService.CreateAsync(
            new CreateBusinessPartnerRequest(
                PartnerNumber,
                Name,
                ShortName,
                null,
                null,
                [new PartnerRoleRequest(RoleType, TradeCategory, null, null)],
                string.IsNullOrWhiteSpace(ContactName)
                    ? []
                    : [new PartnerContactRequest(ContactName, ContactPhone, null, null, true)]),
            cancellationToken);
        return RedirectToPage("/Partners/Index");
    }
}
