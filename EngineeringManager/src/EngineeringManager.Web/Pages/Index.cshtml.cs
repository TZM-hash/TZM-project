using System.Security.Claims;
using EngineeringManager.Application.Dashboard;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages;

public sealed class IndexModel(IDashboardService dashboardService) : PageModel
{
    public DashboardDto? Dashboard { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true) return;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var administrator = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
        var organizationViewer = administrator || User.IsInRole(SystemRoles.Finance) || User.IsInRole(SystemRoles.QueryOnly);
        Dashboard = await dashboardService.GetAsync(new DashboardActor(userId, organizationViewer, organizationViewer, organizationViewer), cancellationToken);
    }
}
