using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Admin;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class OrganizationsModel(IOrganizationService organizationService) : PageModel
{
    public OrganizationOverviewDto Overview { get; private set; } = new([], []);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Overview = await organizationService.GetOverviewAsync(cancellationToken);
    }
}
