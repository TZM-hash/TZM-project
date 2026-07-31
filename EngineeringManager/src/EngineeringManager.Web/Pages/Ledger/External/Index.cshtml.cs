using System.Globalization;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace EngineeringManager.Web.Pages.Ledger.External;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(ICentralLedgerQueryService ledger, IFinanceBusinessYearService years, ApplicationDbContext db) : PageModel
{
    private static readonly HashSet<string> AllowedTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "overview", "receivable", "payable", "collection", "payment", "sales-invoice", "purchase-invoice",
        "deduction", "pending", "exceptions", "payroll", "years", "reconciliation", "audit"
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
    [BindProperty(SupportsGet = true)] public Guid? BusinessPartnerId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ProjectId { get; set; }
    [BindProperty(SupportsGet = true)] public LedgerDirection? Direction { get; set; }
    [BindProperty(SupportsGet = true)] public LedgerSettlementState? SettlementState { get; set; }
    [BindProperty(SupportsGet = true)] public bool HasAdvanceInvoiceCash { get; set; }
    [BindProperty(SupportsGet = true)] public bool HasOverSettlementCash { get; set; }
    [BindProperty(SupportsGet = true)] public bool HasOverInvoiced { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

    public IReadOnlyList<LedgerTabItem> Tabs { get; } =
    [
        new("overview", "总览"),
        new("receivable", "应收款"),
        new("payable", "应付款"),
        new("collection", "收款"),
        new("payment", "付款"),
        new("sales-invoice", "销项发票"),
        new("purchase-invoice", "进项发票"),
        new("deduction", "扣款"),
        new("pending", "待分摊"),
        new("exceptions", "异常"),
        new("payroll", "工资付款"),
        new("years", "年度账"),
        new("reconciliation", "对账"),
        new("audit", "修改日志")
    ];

    public async Task OnGetAsync(CancellationToken token)
    {
        ActiveTab = NormalizeTab(ActiveTab);
        var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
        CanManage = actor.CanManageExternal;
        Years = await years.ListAsync(token);
        Options = await ledger.GetOptionsAsync(actor, LedgerScope.External, token);
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
            ["BusinessPartnerId"] = BusinessPartnerId?.ToString(),
            ["ProjectId"] = ProjectId?.ToString(),
            ["Direction"] = Direction?.ToString(),
            ["SettlementState"] = SettlementState?.ToString(),
            ["HasAdvanceInvoiceCash"] = HasAdvanceInvoiceCash ? "true" : null,
            ["HasOverSettlementCash"] = HasOverSettlementCash ? "true" : null,
            ["HasOverInvoiced"] = HasOverInvoiced ? "true" : null,
            ["Search"] = Search,
            ["PageNumber"] = PageNumber > 1 ? PageNumber.ToString(CultureInfo.InvariantCulture) : null,
            ["PageSize"] = PageSize != 20 ? PageSize.ToString(CultureInfo.InvariantCulture) : null
        };
        return QueryHelpers.AddQueryString("/Ledger/External/Index", values.Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => (string?)item.Value, StringComparer.OrdinalIgnoreCase));
    }

    private CentralLedgerQuery BuildQuery()
    {
        var direction = Direction;
        if (ActiveTab.Equals("receivable", StringComparison.OrdinalIgnoreCase)
            || ActiveTab.Equals("collection", StringComparison.OrdinalIgnoreCase)
            || ActiveTab.Equals("sales-invoice", StringComparison.OrdinalIgnoreCase)) direction = LedgerDirection.Receivable;
        if (ActiveTab.Equals("payable", StringComparison.OrdinalIgnoreCase)
            || ActiveTab.Equals("payment", StringComparison.OrdinalIgnoreCase)
            || ActiveTab.Equals("purchase-invoice", StringComparison.OrdinalIgnoreCase)) direction = LedgerDirection.Payable;

        return new CentralLedgerQuery(
            LedgerScope.External,
            direction,
            FinanceBusinessYearId,
            StartDate,
            EndDate,
            LegalEntityId,
            BusinessPartnerId,
            ProjectId: ProjectId,
            SettlementState: SettlementState,
            HasAdvanceInvoiceCash: HasAdvanceInvoiceCash ? true : null,
            HasOverSettlementCash: HasOverSettlementCash ? true : null,
            HasOverInvoiced: HasOverInvoiced ? true : null,
            Search: Search,
            Page: PageNumber,
            PageSize: PageSize);
    }

    private static string NormalizeTab(string? tab) => AllowedTabs.Contains(tab ?? string.Empty) ? tab!.ToLowerInvariant() : "overview";

    private static CentralLedgerOverviewPageDto Empty() => new([], CentralLedgerMetrics.Zero, 1, 20, 0, 0, [], [], [], 0m, [], [], [], []);

    public sealed record LedgerTabItem(string Key, string Label);
}
