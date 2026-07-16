using EngineeringManager.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Projects;

[Authorize]
public sealed class IndexModel(IProjectService projectService) : PageModel
{
    public IReadOnlyList<ProjectListItemDto> Projects { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Projects = await projectService.ListProjectsAsync(null, null, cancellationToken);
    }
}
