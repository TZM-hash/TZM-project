using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Employees;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IEmployeeService employeeService) : PageModel
{
    public IReadOnlyList<EmployeeDto> Employees { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Employees = await employeeService.ListAsync(null, cancellationToken);
    }
}
