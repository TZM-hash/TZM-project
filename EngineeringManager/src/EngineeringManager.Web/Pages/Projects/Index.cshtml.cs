using System.Security.Claims;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.Projects;
using EngineeringManager.Application.Settings;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Workbenches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Projects;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.SiteStaff + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class IndexModel(
    IProjectService projectService,
    ISavedDataViewService savedViewService,
    IExportService exportService) : PageModel
{
    private static readonly DataViewDefinition ViewDefinition = new(
        "projects",
        new HashSet<string>(["Search", "Stages", "LegalEntityId", "ResponsibleUserId", "MinimumCurrentAmount", "MaximumCurrentAmount"], StringComparer.Ordinal),
        new HashSet<string>(["project_number", "project_name", "stage", "contract_amount", "current_project_amount", "settlement_status"], StringComparer.Ordinal),
        new HashSet<string>(["ProjectNumber", "Name", "Stage", "ContractAmount", "CurrentAmount", "SettlementStatus"], StringComparer.Ordinal));

    public ProjectListPageDto Result { get; private set; } = new([], new ProjectListAggregateDto(0, 0m, 0m, 0), 1, 20, 0, 1, []);
    public DataWorkbenchViewModel Workbench { get; private set; } = null!;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public List<ProjectStage> Stages { get; set; } = [];
    [BindProperty(SupportsGet = true)] public Guid? LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ResponsibleUserId { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MinimumCurrentAmount { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MaximumCurrentAmount { get; set; }
    [BindProperty(SupportsGet = true)] public string SortKey { get; set; } = "ProjectNumber";
    [BindProperty(SupportsGet = true)] public bool SortDescending { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public Guid? SavedViewId { get; set; }
    [BindProperty] public SavedDataViewInput SavedView { get; set; } = new();
    [BindProperty] public List<string> SelectedFields { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveViewAsync(CancellationToken cancellationToken)
    {
        var saved = await savedViewService.SaveAsync(
            UserId(),
            new SaveDataViewRequest(SavedView.Id, "projects", SavedView.Name, SavedView.IsDefault, SavedView.FilterJson, SavedView.ColumnJson, SavedView.SortKey, SavedView.SortDescending, SavedView.RowDensity, SavedView.PageSize),
            ViewDefinition,
            cancellationToken);
        return RedirectToPage(new { savedViewId = saved.Id });
    }

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        var result = await projectService.SearchProjectsAsync(Actor(), Query() with { Page = 1 }, cancellationToken);
        var fields = SelectedFields.Count > 0 ? SelectedFields : ["project_number", "project_name", "stage", "contract_amount", "current_project_amount"];
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

        var actor = Actor();
        Result = await projectService.SearchProjectsAsync(actor, Query(), cancellationToken);
        var options = await projectService.GetListOptionsAsync(actor, cancellationToken);
        Workbench = BuildWorkbench(views, options, selected);
    }

    private ProjectListQuery Query() => new(
        Search,
        Stages,
        LegalEntityId,
        ResponsibleUserId,
        MinimumCurrentAmount,
        MaximumCurrentAmount,
        SortKey,
        SortDescending,
        PageNumber,
        PageSize);

    private ProjectListActor Actor()
    {
        var canAccessAll = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance) || User.IsInRole(SystemRoles.QueryOnly) || User.IsInRole(SystemRoles.EquipmentManager);
        return new ProjectListActor(UserId(), canAccessAll);
    }

    private DataWorkbenchViewModel BuildWorkbench(IReadOnlyList<SavedDataViewDto> views, ProjectListOptionsDto options, SavedDataViewDto? selected)
    {
        var filters = new List<DataWorkbenchFilterField>
        {
            new("Search", "关键词", Search, Placeholder: "项目编号、名称或总包单位"),
            new("Stages", "项目阶段", Stages.Count > 0 ? ((int)Stages[0]).ToString(System.Globalization.CultureInfo.InvariantCulture) : null, DataWorkbenchFilterKind.Select,
                Enum.GetValues<ProjectStage>().Select(value => new DataWorkbenchFilterOption(((int)value).ToString(System.Globalization.CultureInfo.InvariantCulture), StageLabel(value))).ToArray()),
            new("LegalEntityId", "签约公司", LegalEntityId?.ToString(), DataWorkbenchFilterKind.Select,
                options.LegalEntities.Select(item => new DataWorkbenchFilterOption(item.Value, item.Label)).ToArray()),
            new("ResponsibleUserId", "项目负责人", ResponsibleUserId, DataWorkbenchFilterKind.Select,
                options.ResponsibleUsers.Select(item => new DataWorkbenchFilterOption(item.Value, item.Label)).ToArray()),
            new("MinimumCurrentAmount", "最低当前金额", MinimumCurrentAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture), DataWorkbenchFilterKind.Number),
            new("MaximumCurrentAmount", "最高当前金额", MaximumCurrentAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture), DataWorkbenchFilterKind.Number)
        };
        var chips = new List<DataWorkbenchFilterChip>();
        if (!string.IsNullOrWhiteSpace(Search)) chips.Add(new("Search", "关键词", Search));
        if (Stages.Count > 0) chips.Add(new("Stages", "阶段", string.Join("、", Stages.Select(StageLabel))));
        if (LegalEntityId.HasValue) chips.Add(new("LegalEntityId", "签约公司", options.LegalEntities.FirstOrDefault(item => item.Value == LegalEntityId.Value.ToString())?.Label ?? LegalEntityId.Value.ToString()));
        if (!string.IsNullOrWhiteSpace(ResponsibleUserId)) chips.Add(new("ResponsibleUserId", "负责人", options.ResponsibleUsers.FirstOrDefault(item => item.Value == ResponsibleUserId)?.Label ?? ResponsibleUserId));
        if (MinimumCurrentAmount.HasValue) chips.Add(new("MinimumCurrentAmount", "最低金额", MinimumCurrentAmount.Value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture)));
        if (MaximumCurrentAmount.HasValue) chips.Add(new("MaximumCurrentAmount", "最高金额", MaximumCurrentAmount.Value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture)));

        return new DataWorkbenchViewModel(
            "projects",
            "projects-table",
            [
                new("project_number", "项目编号", true, true),
                new("project_name", "项目名称"),
                new("stage", "阶段"),
                new("contract_amount", "合同金额"),
                new("current_project_amount", "当前工程金额"),
                new("settlement_status", "结算状态")
            ],
            filters,
            chips,
            views,
            selected?.RowDensity ?? TableDensity.Standard,
            PageSize,
            SortKey,
            SortDescending,
            selected?.Id,
            true);
    }

    private void ApplySavedView(SavedDataViewDto view)
    {
        var filters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(view.FilterJson) ?? [];
        Search = ReadString(filters, "Search") ?? Search;
        if (TryReadStage(ReadString(filters, "Stages"), out var parsedStage)) Stages = [parsedStage];
        if (Guid.TryParse(ReadString(filters, "LegalEntityId"), out var legalEntityId)) LegalEntityId = legalEntityId;
        ResponsibleUserId = ReadString(filters, "ResponsibleUserId") ?? ResponsibleUserId;
        if (decimal.TryParse(ReadString(filters, "MinimumCurrentAmount"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var minimum)) MinimumCurrentAmount = minimum;
        if (decimal.TryParse(ReadString(filters, "MaximumCurrentAmount"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var maximum)) MaximumCurrentAmount = maximum;
        SortKey = view.SortKey ?? SortKey;
        SortDescending = view.SortDescending;
        PageSize = view.PageSize;
    }

    private static string? ReadString(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value) ? value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().FirstOrDefault().ToString() : value.ToString() : null;

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("当前用户没有标识。");

    private static string StageLabel(ProjectStage stage) => stage switch
    {
        ProjectStage.Preliminary => "前期跟踪",
        ProjectStage.AwaitingContract => "待签合同",
        ProjectStage.AwaitingMobilization => "待进场",
        ProjectStage.UnderConstruction => "施工中",
        ProjectStage.Suspended => "已停工",
        ProjectStage.CompletedAwaitingAcceptance => "完工待验收",
        ProjectStage.Settlement => "结算中",
        ProjectStage.Warranty => "质保期",
        ProjectStage.Closed => "已关闭",
        _ => stage.ToString()
    };

    private static bool TryReadStage(string? value, out ProjectStage stage)
    {
        if (Enum.TryParse(value, true, out stage)) return true;
        if (int.TryParse(value, out var number) && Enum.IsDefined(typeof(ProjectStage), number))
        {
            stage = (ProjectStage)number;
            return true;
        }
        stage = default;
        return false;
    }
}
