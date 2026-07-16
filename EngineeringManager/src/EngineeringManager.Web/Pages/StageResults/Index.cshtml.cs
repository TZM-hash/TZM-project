using EngineeringManager.Application.StageResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.StageResults;

[Authorize]
public sealed class IndexModel(IStageResultService stageResultService) : PageModel
{
    public IReadOnlyList<StageResultDto> Results { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Results = await stageResultService.ListByProjectAsync(null, cancellationToken);
    }
}
