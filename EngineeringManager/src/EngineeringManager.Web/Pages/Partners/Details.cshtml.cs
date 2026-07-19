using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Partners;

[Authorize]
public sealed class DetailsModel(IBusinessPartnerService service) : PageModel
{
    public BusinessPartnerDto Partner { get; private set; } = null!;
    public bool CanManageFinance => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken token)
    {
        var partner = await service.GetAsync(id, token);
        if (partner is null) return NotFound();
        Partner = partner;
        return Page();
    }
}
