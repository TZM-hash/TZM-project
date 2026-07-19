using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Ledger.Reconciliations;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class DetailsModel(IFinanceReconciliationService service, ApplicationDbContext db) : PageModel
{
    public FinanceReconciliationDetailsDto Details { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken token)
    {
        var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
        var details = await service.GetDetailsAsync(actor, id, token);
        if (details is null) return NotFound();
        Details = details;
        return Page();
    }
}
