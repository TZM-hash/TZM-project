using System.Security.Claims;
using EngineeringManager.Application.TemporaryWorkers;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.TemporaryWorkers;

[Authorize]
public sealed class DetailsModel(ITemporaryWorkerService service, ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid EmployeeId { get; set; }
    [BindProperty] public string ConversionReason { get; set; } = "临时人员转为员工";
    public TemporaryWorkerDto Worker { get; private set; } = null!;
    public IReadOnlyList<EmployeeOption> Employees { get; private set; } = [];
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance);
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken) ? Page() : NotFound();
    public async Task<IActionResult> OnPostConvertAsync(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await service.LinkConvertedEmployeeAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", Id, EmployeeId, ConversionReason, cancellationToken);
        return RedirectToPage(new { id = Id });
    }
    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var worker = await service.GetAsync(Id, CanManage, cancellationToken);
        if (worker is null) return false;
        Worker = worker;
        Employees = await db.Employees.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.EmployeeNumber).Select(item => new EmployeeOption(item.Id, item.EmployeeNumber + " · " + item.Name)).ToListAsync(cancellationToken);
        return true;
    }
    public sealed record EmployeeOption(Guid Id, string Label);
}
