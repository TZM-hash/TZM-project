using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Payroll;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IPayrollService payrollService) : PageModel
{
    public PayrollDisbursementOverviewDto Overview { get; private set; } = new(0m, 0m, 0m, 0m, []);
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    public bool CanViewSensitive => PageContext?.HttpContext?.User?.IsInRole(SystemRoles.SystemAdministrator) == true
        || PageContext?.HttpContext?.User?.IsInRole(SystemRoles.ApplicationAdministrator) == true;

    public async Task OnGetAsync(CancellationToken cancellationToken) => Overview = string.IsNullOrWhiteSpace(Search)
        ? await payrollService.GetDisbursementOverviewAsync(cancellationToken)
        : await payrollService.SearchDisbursementOverviewAsync(Search, CanViewSensitive, cancellationToken);
}
