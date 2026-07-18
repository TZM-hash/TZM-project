using System.Security.Claims;
using EngineeringManager.Application.TemporaryWorkers;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.TemporaryWorkers;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(ITemporaryWorkerService service) : PageModel
{
    [BindProperty] public CreateTemporaryWorkerRequest Input { get; set; } = new("", null, null, null, null, null, null, null, "新增临时人员");
    public IReadOnlyList<TemporaryWorkerDto> Workers { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await service.CreateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", Input, cancellationToken);
        return RedirectToPage();
    }
    private async Task LoadAsync(CancellationToken cancellationToken) => Workers = await service.ListAsync(false, CanManage, cancellationToken);
}
