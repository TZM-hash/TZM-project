using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Ledger.Internal;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(ICentralLedgerQueryService ledger, IFinanceBusinessYearService years, ApplicationDbContext db) : PageModel
{
    public CentralLedgerOverviewPageDto Result { get; private set; } = new([], CentralLedgerMetrics.Zero, 1, 20, 0, 0, []);
    public CentralLedgerOptionsDto Options { get; private set; } = new([], [], [], [], [], [], [], [], []);
    public IReadOnlyList<FinanceBusinessYearDto> Years { get; private set; } = [];
    public bool CanManage { get; private set; }

    [BindProperty(SupportsGet = true)] public Guid? FinanceBusinessYearId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? EndDate { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CounterLegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public LedgerDirection? Direction { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public async Task OnGetAsync(CancellationToken token)
    {
        var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
        CanManage = actor.CanManageInternal;
        Years = await years.ListAsync(token);
        Options = await ledger.GetOptionsAsync(actor, LedgerScope.Internal, token);
        Result = await ledger.SearchAsync(actor, new CentralLedgerQuery(
            LedgerScope.Internal, Direction, FinanceBusinessYearId, StartDate, EndDate, LegalEntityId,
            CounterLegalEntityId: CounterLegalEntityId, Search: Search, Page: PageNumber), token);
    }
}
