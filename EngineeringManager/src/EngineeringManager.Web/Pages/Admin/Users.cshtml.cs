using EngineeringManager.Application.Users;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Admin;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator)]
public sealed class UsersModel(IUserAdministrationService userAdministrationService) : PageModel
{
    public IReadOnlyList<UserAdminDto> Users { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Users = await userAdministrationService.GetUsersAsync(cancellationToken);
    }
}
