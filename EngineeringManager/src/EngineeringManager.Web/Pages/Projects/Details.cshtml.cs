using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Projects;

[Authorize]
public sealed class DetailsModel(IProjectWorkspaceService workspaceService) : PageModel
{
    public ProjectWorkspaceDto? Workspace { get; private set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.ProjectManager);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        Workspace = await workspaceService.GetAsync(id, cancellationToken);
        return Page();
    }
}
