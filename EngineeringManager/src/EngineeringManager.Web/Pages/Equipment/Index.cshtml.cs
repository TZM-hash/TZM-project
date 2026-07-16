using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.SiteStaff + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class IndexModel(IEquipmentService service) : EquipmentPageModel
{
    public EquipmentDashboardDto Dashboard { get; private set; } = new(0, 0, 0, 0, 0, new Dictionary<string, int>(), []);
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public EquipmentStatus? Status { get; set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.EquipmentManager);
    public async Task OnGetAsync(CancellationToken token) => Dashboard = await service.GetDashboardAsync(ResolveActor(), new EquipmentFilter(null, null, Status, Keyword), token);
}
