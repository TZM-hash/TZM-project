using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace EngineeringManager.Web.Pages.Equipment;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class DetailsModel(IEquipmentService service) : EquipmentPageModel
{
    public EquipmentDetailsDto Equipment { get; private set; } = null!;
    public async Task OnGetAsync(Guid id, CancellationToken token)
    {
        var dashboard = await service.GetDashboardAsync(ResolveActor(), new EquipmentFilter(null, null, null, null), token);
        Equipment = dashboard.Items.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("设备不存在或无权访问。");
    }
}
