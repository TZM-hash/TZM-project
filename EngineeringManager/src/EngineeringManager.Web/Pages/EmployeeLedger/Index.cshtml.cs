using EngineeringManager.Application.EmployeeLedger;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.EmployeeLedger;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IEmployeeLedgerService employeeLedgerService) : PageModel
{
    public EmployeeLedgerOverviewDto Overview { get; private set; } = new(0m, 0m, 0m, 0m, 0m, 0m, 0m, false, []);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Overview = await employeeLedgerService.GetOverviewAsync(cancellationToken);
    }
}
