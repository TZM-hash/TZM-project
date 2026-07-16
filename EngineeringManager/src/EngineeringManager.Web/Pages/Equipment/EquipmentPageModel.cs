using System.Security.Claims;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Equipment;

public abstract class EquipmentPageModel : PageModel
{
    protected EquipmentActor ResolveActor()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("用户身份无效。");
        var administrator = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
        var manager = administrator || User.IsInRole(SystemRoles.EquipmentManager);
        return new EquipmentActor(userId, manager, administrator || User.IsInRole(SystemRoles.EquipmentManager) || User.IsInRole(SystemRoles.Finance), administrator, administrator || User.IsInRole(SystemRoles.EquipmentManager), [], []);
    }
}
