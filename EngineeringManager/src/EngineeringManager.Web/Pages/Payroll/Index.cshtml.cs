using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Payroll;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IPayrollService payrollService) : PageModel
{
    public PayrollDisbursementOverviewDto Overview { get; private set; } = new(0m, 0m, 0m, 0m, 0m, []);

    public async Task OnGetAsync(CancellationToken cancellationToken) => Overview = await payrollService.GetDisbursementOverviewAsync(cancellationToken);
}
