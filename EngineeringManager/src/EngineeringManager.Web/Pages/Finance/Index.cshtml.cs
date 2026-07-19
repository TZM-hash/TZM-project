using System.Security.Claims;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Settings;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Workbenches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Finance;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.QueryOnly)]
public sealed class IndexModel(
    IFinanceLedgerService financeService,
    ISavedDataViewService savedViewService,
    IExportService exportService) : PageModel
{
    private static readonly DataViewDefinition ViewDefinition = new(
        "finance",
        new HashSet<string>(["Search", "MinimumReceivable", "MinimumUncollected", "RiskOnly"], StringComparer.Ordinal),
        new HashSet<string>(["project_number", "project_name", "receivable_amount", "collected_amount", "uncollected_amount", "payable_amount", "paid_amount", "unpaid_amount", "output_invoice_amount", "uninvoiced_amount", "risk"], StringComparer.Ordinal),
        new HashSet<string>(["ProjectNumber", "ProjectName", "ReceivableAmount", "CollectedAmount", "UncollectedAmount", "PayableAmount", "PaidAmount", "UnpaidAmount", "OutputInvoiceAmount", "UninvoicedAmount"], StringComparer.Ordinal));

    public FinanceOverviewPageDto Result { get; private set; } = new([], EmptySummary(), 1, 20, 0, 1, []);
    public DataWorkbenchViewModel Workbench { get; private set; } = null!;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MinimumReceivable { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MinimumUncollected { get; set; }
    [BindProperty(SupportsGet = true)] public bool RiskOnly { get; set; }
    [BindProperty(SupportsGet = true)] public string SortKey { get; set; } = "ProjectNumber";
    [BindProperty(SupportsGet = true)] public bool SortDescending { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public Guid? SavedViewId { get; set; }
    [BindProperty] public SavedDataViewInput SavedView { get; set; } = new();
    [BindProperty] public List<string> SelectedFields { get; set; } = [];

    public IActionResult OnGet() => RedirectToPage("/Ledger/External/Index");

    public async Task<IActionResult> OnPostSaveViewAsync(CancellationToken cancellationToken)
    {
        var saved = await savedViewService.SaveAsync(
            UserId(),
            new SaveDataViewRequest(SavedView.Id, "finance", SavedView.Name, SavedView.IsDefault, SavedView.FilterJson, SavedView.ColumnJson, SavedView.SortKey, SavedView.SortDescending, SavedView.RowDensity, SavedView.PageSize),
            ViewDefinition,
            cancellationToken);
        return RedirectToPage(new { savedViewId = saved.Id });
    }

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        var result = await financeService.SearchOverviewAsync(Query() with { Page = 1 }, cancellationToken);
        var fields = SelectedFields.Count > 0 ? SelectedFields : ["project_number", "project_name", "receivable_amount", "collected_amount", "uncollected_amount", "payable_amount", "paid_amount", "unpaid_amount", "output_invoice_amount", "uninvoiced_amount"];
        var file = await exportService.ExportAsync(new ExportRequest(ExportDataset.ProjectOverview, UserId(), fields, null, result.MatchingProjectIds), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    public string PageUrl(int page)
    {
        var pairs = Request.Query.SelectMany(item => item.Value.Select(value => new KeyValuePair<string, string?>(item.Key, value)))
            .Where(item => !string.Equals(item.Key, nameof(PageNumber), StringComparison.OrdinalIgnoreCase))
            .Append(new KeyValuePair<string, string?>(nameof(PageNumber), page.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return $"{Request.Path}{QueryString.Create(pairs)}";
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var views = await savedViewService.ListAsync(UserId(), ViewDefinition, cancellationToken);
        var selected = SavedViewId.HasValue ? views.FirstOrDefault(item => item.Id == SavedViewId) : Request.Query.Count == 0 ? views.FirstOrDefault(item => item.IsDefault) : null;
        if (selected is not null)
        {
            SavedViewId = selected.Id;
            ApplySavedView(selected);
        }
        Result = await financeService.SearchOverviewAsync(Query(), cancellationToken);
        Workbench = BuildWorkbench(views, selected);
    }

    private FinanceOverviewQuery Query() => new(Search, MinimumReceivable, MinimumUncollected, RiskOnly, SortKey, SortDescending, PageNumber, PageSize);

    private DataWorkbenchViewModel BuildWorkbench(IReadOnlyList<SavedDataViewDto> views, SavedDataViewDto? selected)
    {
        var filters = new List<DataWorkbenchFilterField>
        {
            new("Search", "项目关键词", Search, Placeholder: "项目编号或名称"),
            new("MinimumReceivable", "最低应收款", MinimumReceivable?.ToString(System.Globalization.CultureInfo.InvariantCulture), DataWorkbenchFilterKind.Number),
            new("MinimumUncollected", "最低未收款", MinimumUncollected?.ToString(System.Globalization.CultureInfo.InvariantCulture), DataWorkbenchFilterKind.Number),
            new("RiskOnly", "风险状态", RiskOnly ? "true" : null, DataWorkbenchFilterKind.Select,
                [new DataWorkbenchFilterOption("true", "只看超收/超付风险")])
        };
        var chips = new List<DataWorkbenchFilterChip>();
        if (!string.IsNullOrWhiteSpace(Search)) chips.Add(new("Search", "关键词", Search));
        if (MinimumReceivable.HasValue) chips.Add(new("MinimumReceivable", "最低应收", MinimumReceivable.Value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture)));
        if (MinimumUncollected.HasValue) chips.Add(new("MinimumUncollected", "最低未收", MinimumUncollected.Value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture)));
        if (RiskOnly) chips.Add(new("RiskOnly", "风险", "超收/超付"));

        return new DataWorkbenchViewModel(
            "finance",
            "finance-table",
            [
                new("project_number", "项目编号", true, true), new("project_name", "项目名称"),
                new("receivable_amount", "应收"), new("collected_amount", "已收"), new("uncollected_amount", "未收"),
                new("payable_amount", "应付"), new("paid_amount", "已付"), new("unpaid_amount", "未付"),
                new("output_invoice_amount", "已开票"), new("uninvoiced_amount", "未开票"), new("risk", "风险")
            ],
            filters,
            chips,
            views,
            selected?.RowDensity ?? TableDensity.Standard,
            PageSize,
            SortKey,
            SortDescending,
            selected?.Id,
            true,
            InlineFilters: [filters[0]]);
    }

    private void ApplySavedView(SavedDataViewDto view)
    {
        var filters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(view.FilterJson) ?? [];
        Search = ReadString(filters, "Search") ?? Search;
        if (decimal.TryParse(ReadString(filters, "MinimumReceivable"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var receivable)) MinimumReceivable = receivable;
        if (decimal.TryParse(ReadString(filters, "MinimumUncollected"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var uncollected)) MinimumUncollected = uncollected;
        if (bool.TryParse(ReadString(filters, "RiskOnly"), out var riskOnly)) RiskOnly = riskOnly;
        SortKey = view.SortKey ?? SortKey;
        SortDescending = view.SortDescending;
        PageSize = view.PageSize;
    }

    private static string? ReadString(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value) ? value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().FirstOrDefault().ToString() : value.ToString() : null;

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("当前用户没有标识。");
    private static FinanceProjectSummaryDto EmptySummary() => new(Guid.Empty, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, false, false);
}
