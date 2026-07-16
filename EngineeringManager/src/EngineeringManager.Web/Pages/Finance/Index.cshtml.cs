using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Finance;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IFinanceLedgerService financeService) : PageModel
{
    public FinanceOverviewDto Overview { get; private set; } = new([], new FinanceProjectSummaryDto(Guid.Empty, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, false, false));

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Overview = await financeService.GetOverviewAsync(cancellationToken);
    }
}
