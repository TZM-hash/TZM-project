using EngineeringManager.Application.ConstructionCrews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Crews;

[Authorize]
public sealed class IndexModel(IConstructionCrewService crewService) : PageModel
{
    public IReadOnlyList<ConstructionCrewListItemDto> Crews { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken cancellationToken) => Crews = await crewService.ListAsync(false, cancellationToken);
}
