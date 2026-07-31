using System.Globalization;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace EngineeringManager.Web.Pages.Ledger.Internal;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(ICentralLedgerQueryService ledger, IFinanceBusinessYearService years, ApplicationDbContext db) : PageModel
{
    private static readonly HashSet<string> AllowedTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "overview", "receivable", "payable", "transfer", "invoice", "pending", "years", "reconciliation", "audit"
    };

    public CentralLedgerOverviewPageDto Result { get; private set; } = Empty();
    public CentralLedgerOptionsDto Options { get; private set; } = new([], [], [], [], [], [], [], [], []);
    public IReadOnlyList<FinanceBusinessYearDto> Years { get; private set; } = [];
    public bool CanManage { get; private set; }

    [BindProperty(Name = "view", SupportsGet = true)] public string ActiveTab { get; set; } = "overview";
    [BindProperty(SupportsGet = true)] public Guid? FinanceBusinessYearId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? EndDate { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CounterLegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public LedgerDirection? Direction { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public IReadOnlyList<LedgerTabItem> Tabs { get; } =
    [
        new("overview", "总览"),
        new("receivable", "内部应收"),
        new("payable", "内部应付"),
        new("transfer", "内部转账"),
        new("invoice", "内部发票"),
        new("pending", "待分摊"),
        new("years", "年度账"),
        new("reconciliation", "对账"),
        new("audit", "修改记录")
    ];

    public async Task OnGetAsync(CancellationToken token)
    {
        ActiveTab = NormalizeTab(ActiveTab);
        var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
        CanManage = actor.CanManageInternal;
        Years = await years.ListAsync(token);
        Options = await ledger.GetOptionsAsync(actor, LedgerScope.Internal, token);
        Result = await ledger.SearchAsync(actor, BuildQuery(), token);
    }

    public async Task<IActionResult> OnGetDetailsAsync(string type, Guid id, CancellationToken token)
    {
        if (!Enum.TryParse<FinanceRecordType>(type, true, out var recordType)) return BadRequest("不支持的财务记录类型。");
        var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
        var details = await ledger.GetAsync(actor, recordType, id, token);
        return details is null ? NotFound() : new JsonResult(details);
    }

    public string TabUrl(string tab)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["view"] = NormalizeTab(tab),
            ["FinanceBusinessYearId"] = FinanceBusinessYearId?.ToString(),
            ["StartDate"] = StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["EndDate"] = EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["LegalEntityId"] = LegalEntityId?.ToString(),
            ["CounterLegalEntityId"] = CounterLegalEntityId?.ToString(),
            ["Direction"] = Direction?.ToString(),
            ["Search"] = Search,
            ["PageNumber"] = PageNumber > 1 ? PageNumber.ToString(CultureInfo.InvariantCulture) : null,
            ["PageSize"] = PageSize != 20 ? PageSize.ToString(CultureInfo.InvariantCulture) : null
        };
        return QueryHelpers.AddQueryString("/Ledger/Internal/Index", values.Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => (string?)item.Value, StringComparer.OrdinalIgnoreCase));
    }

    private CentralLedgerQuery BuildQuery()
    {
        var direction = Direction;
        if (ActiveTab.Equals("receivable", StringComparison.OrdinalIgnoreCase)) direction = LedgerDirection.Receivable;
        if (ActiveTab.Equals("payable", StringComparison.OrdinalIgnoreCase)) direction = LedgerDirection.Payable;
        return new CentralLedgerQuery(
            LedgerScope.Internal,
            direction,
            FinanceBusinessYearId,
            StartDate,
            EndDate,
            LegalEntityId,
            CounterLegalEntityId: CounterLegalEntityId,
            Search: Search,
            Page: PageNumber,
            PageSize: PageSize);
    }

    private static string NormalizeTab(string? tab) => AllowedTabs.Contains(tab ?? string.Empty) ? tab!.ToLowerInvariant() : "overview";

    private static CentralLedgerOverviewPageDto Empty() => new([], CentralLedgerMetrics.Zero, 1, 20, 0, 0, [], [], [], 0m, [], [], [], []);

    public sealed record LedgerTabItem(string Key, string Label);
}
