using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Ledger.Reconciliations;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(IFinanceReconciliationService service, IFinanceBusinessYearService years, ICentralLedgerQueryService ledger, ApplicationDbContext db) : PageModel
{
    public IReadOnlyList<FinanceReconciliationDto> Items { get; private set; } = [];
    public IReadOnlyList<FinanceBusinessYearDto> Years { get; private set; } = [];
    public CentralLedgerOptionsDto Options { get; private set; } = new([], [], [], [], [], [], [], [], []);
    public bool CanManage { get; private set; }
    [BindProperty(SupportsGet = true)] public LedgerScope Scope { get; set; } = LedgerScope.External;
    [BindProperty(SupportsGet = true)] public Guid? FinanceBusinessYearId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessPartnerId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly AsOfDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [BindProperty] public FinanceReconciliationScope ReconciliationScope { get; set; } = FinanceReconciliationScope.WholeLedger;
    [BindProperty] public Guid DeleteId { get; set; }
    [BindProperty] public Guid DeleteConcurrencyStamp { get; set; }
    [BindProperty] public string DeleteReason { get; set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken token) => await LoadAsync(token);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken token)
    {
        try
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
            var query = new CentralLedgerQuery(Scope, FinanceBusinessYearId: FinanceBusinessYearId, StartDate: StartDate, EndDate: AsOfDate, LegalEntityId: LegalEntityId, BusinessPartnerId: BusinessPartnerId);
            var id = await service.CreateAsync(actor, new CreateFinanceReconciliationRequest(
                Scope, ReconciliationScope, FinanceBusinessYearId, LegalEntityId, BusinessPartnerId, StartDate, AsOfDate, query), token);
            return RedirectToPage("/Ledger/Reconciliations/Details", new { id });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            TempData["Error"] = exception.Message;
            return RedirectToPage(new { Scope, FinanceBusinessYearId, LegalEntityId, BusinessPartnerId, StartDate, AsOfDate });
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken token)
    {
        try
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
            await service.DeleteAsync(actor, DeleteId, DeleteConcurrencyStamp, DeleteReason, token);
            TempData["Success"] = "对账快照已删除。";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToPage(new { Scope });
    }

    private async Task LoadAsync(CancellationToken token)
    {
        var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
        CanManage = actor.CanReconcile;
        Years = await years.ListAsync(token);
        Options = await ledger.GetOptionsAsync(actor, Scope, token);
        Items = await service.ListAsync(actor, new CentralLedgerQuery(
            Scope, FinanceBusinessYearId: FinanceBusinessYearId, StartDate: StartDate, EndDate: AsOfDate,
            LegalEntityId: LegalEntityId, BusinessPartnerId: BusinessPartnerId), token);
    }
}
