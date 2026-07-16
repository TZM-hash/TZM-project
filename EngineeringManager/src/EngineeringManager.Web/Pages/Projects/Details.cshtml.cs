using EngineeringManager.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Projects;

[Authorize]
public sealed class DetailsModel(IProjectService projectService) : PageModel
{
    public ProjectDetailsDto? Details { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        Details = await projectService.GetProjectAsync(id, cancellationToken);
        return Page();
    }
}
