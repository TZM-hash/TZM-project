using System.Security.Claims;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Ledger;

internal static class LedgerPageSupport
{
    public static async Task<CentralLedgerActor> CreateActorAsync(ClaimsPrincipal user, ApplicationDbContext db, CancellationToken token)
    {
        var isAdministrator = user.IsInRole(SystemRoles.SystemAdministrator) || user.IsInRole(SystemRoles.ApplicationAdministrator);
        var isFinance = user.IsInRole(SystemRoles.Finance);
        return new CentralLedgerActor(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.Identity?.Name ?? "ledger-user",
            user.Identity?.Name,
            (await db.LegalEntities.AsNoTracking().Select(item => item.Id).ToListAsync(token)).ToHashSet(),
            (await db.Projects.AsNoTracking().Select(item => item.Id).ToListAsync(token)).ToHashSet(),
            isAdministrator || isFinance,
            isAdministrator || isFinance,
            isAdministrator || isFinance,
            isAdministrator || isFinance);
    }
}
