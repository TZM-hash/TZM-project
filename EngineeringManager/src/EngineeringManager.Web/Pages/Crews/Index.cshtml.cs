using EngineeringManager.Application.ConstructionCrews;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Crews;

[Authorize]
public sealed class IndexModel(IConstructionCrewService crewService) : PageModel
{
    public IReadOnlyList<ConstructionCrewListItemDto> Crews { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    public bool CanViewSensitive => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public async Task OnGetAsync(CancellationToken cancellationToken) => Crews = await crewService.ListAsync(false, Search, CanViewSensitive, cancellationToken);
}
