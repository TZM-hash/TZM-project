using System.Security.Claims;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Application.Settings;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Presentation;
using EngineeringManager.Web.Workbenches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Projects;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.SiteStaff + "," + SystemRoles.QueryOnly + "," + SystemRoles.EquipmentManager)]
public sealed class IndexModel(
    IProjectService projectService,
    IFinanceLedgerService financeService,
    ISavedDataViewService savedViewService,
    IProjectWorkbookService projectWorkbookService) : PageModel
{
    private const string ExportViewKey = "projects-export";
    private const string ExportViewName = "项目清单导出列";
    private const string NoExportableProjectsMessage = "没有可导出的项目。";
    private const string ExportColumnModeContent = "content";
    private const string ExportColumnModeTable = "table";
    private static readonly DataWorkbenchColumn[] ProjectExportColumnDefinitions =
    [
        new("serial_number", "序号"),
        new("project_number", "项目编号"),
        new("project_name", "项目名称"),
        new("stage", "阶段"),
        new("contract_signing_status", "合同签订"),
        new("affiliation_type", "合作方式"),
        new("parent_project", "上级项目"),
        new("general_contractor", "总包单位"),
        new("general_contractor_contact", "总包联系人 / 电话"),
        new("responsible_user", "项目负责人"),
        new("department", "部门"),
        new("branch", "分支机构"),
        new("legal_entities", "签约公司"),
        new("actual_start_date", "实际开始日期"),
        new("actual_completion_date", "实际完工日期"),
        new("contract_amount", "合同金额"),
        new("estimated_amount", "预计金额"),
        new("settled_amount", "已结算金额"),
        new("current_project_amount", "当前工程金额"),
        new("settlement_status", "结算状态"),
        new("contract_count", "合同数量"),
        new("line_item_count", "清单项数量"),
        new("collection_rate", "收款率"),
        new("collection_receivable_amount", "应收金额"),
        new("collection_collected_amount", "已收金额"),
        new("collection_uncollected_amount", "未收金额"),
        new("payment_rate", "付款率"),
        new("payment_payable_amount", "应付金额"),
        new("payment_paid_amount", "已付金额"),
        new("payment_unpaid_amount", "未付金额"),
        new("invoice_rate", "开票率"),
        new("invoice_invoiced_amount", "已开票金额"),
        new("invoice_uninvoiced_amount", "未开票金额"),
        new("notes", "备注摘要")
    ];
    private static readonly string[] ProjectExportColumnKeys = ProjectExportColumnDefinitions.Select(column => column.Key).ToArray();

    private static readonly DataViewDefinition ViewDefinition = new(
        "projects",
        new HashSet<string>(["Search", "Stages", "LegalEntityId", "ResponsibleUserId", "ResponsibleEmployeeId", "AffiliationType", "MinimumCurrentAmount", "MaximumCurrentAmount"], StringComparer.Ordinal),
        new HashSet<string>(ProjectExportColumnKeys.Append("actions"), StringComparer.Ordinal),
        new HashSet<string>(["ProjectNumber", "Name", "Stage", "ContractAmount", "CurrentAmount", "SettlementStatus"], StringComparer.Ordinal));

    private static readonly DataViewDefinition ExportViewDefinition = new(
        ExportViewKey,
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(ProjectExportColumnKeys, StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    public ProjectListPageDto Result { get; private set; } = new([], new ProjectListAggregateDto(0, 0m, 0m, 0), 1, 20, 0, 1, []);
    public FinanceProjectSummaryDto FinanceTotal { get; private set; } = EmptyFinanceSummary();
    public IReadOnlyDictionary<Guid, FinanceProjectSummaryDto> FinanceByProjectId { get; private set; } = new Dictionary<Guid, FinanceProjectSummaryDto>();
    public DataWorkbenchViewModel Workbench { get; private set; } = null!;
    public IReadOnlyList<DataWorkbenchColumn> ProjectExportColumns => ProjectExportColumnDefinitions;
    public IReadOnlyList<DataWorkbenchFilterField> ProjectExportFilters { get; private set; } = [];
    public bool UsesTableExportColumns => string.Equals(NormalizeExportColumnMode(ExportColumnMode), ExportColumnModeTable, StringComparison.Ordinal);
    public bool UsesContentExportColumns => !UsesTableExportColumns;
    public bool CanExportWorkbook => WorkbookActor().CanExport;
    public bool CanExportFullWorkbook => WorkbookActor().CanExportFullWorkbook;
    public bool CanExportWorkbookAttachments => WorkbookActor().CanExportAttachments;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public List<ProjectStage> Stages { get; set; } = [];
    [BindProperty(SupportsGet = true)] public Guid? LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ResponsibleUserId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ResponsibleEmployeeId { get; set; }
    [BindProperty(SupportsGet = true)] public ProjectAffiliationType? AffiliationType { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MinimumCurrentAmount { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MaximumCurrentAmount { get; set; }
    [BindProperty(SupportsGet = true)] public string SortKey { get; set; } = "ProjectNumber";
    [BindProperty(SupportsGet = true)] public bool SortDescending { get; set; } = true;
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public Guid? SavedViewId { get; set; }
    [BindProperty] public SavedDataViewInput SavedView { get; set; } = new();
    [BindProperty] public List<Guid> SelectedProjectIds { get; set; } = [];
    [BindProperty] public bool SelectAllMatching { get; set; }
    [BindProperty] public List<string> ExportColumns { get; set; } = [];
    [BindProperty] public List<string> TableExportColumns { get; set; } = [];
    [BindProperty] public string ExportColumnMode { get; set; } = ExportColumnModeContent;
    [BindProperty] public bool ExportFiltersInitialized { get; set; }
    [BindProperty] public string? ExportSearch { get; set; }
    [BindProperty] public List<ProjectStage> ExportStages { get; set; } = [];
    [BindProperty] public Guid? ExportLegalEntityId { get; set; }
    [BindProperty] public string? ExportResponsibleUserId { get; set; }
    [BindProperty] public Guid? ExportResponsibleEmployeeId { get; set; }
    [BindProperty] public ProjectAffiliationType? ExportAffiliationType { get; set; }
    [BindProperty] public decimal? ExportMinimumCurrentAmount { get; set; }
    [BindProperty] public decimal? ExportMaximumCurrentAmount { get; set; }
    [BindProperty] public bool IncludeWorkbookAttachments { get; set; }

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

    public async Task<IActionResult> OnPostExportWorkbookAsync(CancellationToken cancellationToken)
    {
        if (!CanExportWorkbook) return Forbid();
        ExportColumnMode = NormalizeExportColumnMode(ExportColumnMode);
        if (SelectedProjectIds.Count > 0) SelectAllMatching = false;
        if (!SelectAllMatching && SelectedProjectIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "请至少勾选一个项目，或选择导出当前筛选命中的全部项目。");
            await LoadAsync(cancellationToken, loadExportColumns: false, applyDefaultSavedView: false);
            return Page();
        }
        var requestedColumns = UsesTableExportColumns ? TableExportColumns : ExportColumns;
        ExportColumns = NormalizeExportColumns(requestedColumns).ToList();
        if (ExportColumns.Count == 0)
        {
            ModelState.AddModelError(nameof(ExportColumns), "请至少选择一列导出。");
            await LoadAsync(cancellationToken, loadExportColumns: false, applyDefaultSavedView: false);
            return Page();
        }
        if (UsesContentExportColumns) await SaveExportColumnsAsync(cancellationToken);
        var exportQuery = SelectAllMatching ? ExportQuery() : Query();
        var request = new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(Actor(), exportQuery, SelectAllMatching, SelectedProjectIds),
            [ProjectWorkbookSheet.ProjectMaster],
            IncludeAttachments: IncludeWorkbookAttachments,
            Actor: WorkbookActor(),
            ProjectListColumns: ExportColumns);
        try
        {
            var file = await projectWorkbookService.ExportAsync(request, cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (InvalidOperationException exception) when (string.Equals(exception.Message, NoExportableProjectsMessage, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "当前筛选没有可导出的项目，请调整筛选条件后重试。");
            await LoadAsync(cancellationToken, loadExportColumns: false, applyDefaultSavedView: false);
            return Page();
        }
    }

    public string PageUrl(int page)
    {
        var pairs = Request.Query.SelectMany(item => item.Value.Select(value => new KeyValuePair<string, string?>(item.Key, value)))
            .Where(item => !string.Equals(item.Key, nameof(PageNumber), StringComparison.OrdinalIgnoreCase))
            .Append(new KeyValuePair<string, string?>(nameof(PageNumber), page.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return $"{Request.Path}{QueryString.Create(pairs)}";
    }

    private async Task LoadAsync(CancellationToken cancellationToken, bool loadExportColumns = true, bool applyDefaultSavedView = true)
    {
        PageSize = NormalizePageSize(PageSize);
        PageNumber = Math.Max(1, PageNumber);
        var views = await savedViewService.ListAsync(UserId(), ViewDefinition, cancellationToken);
        if (loadExportColumns && CanExportWorkbook) await LoadExportColumnsAsync(cancellationToken);
        var selected = SavedViewId.HasValue
            ? views.FirstOrDefault(item => item.Id == SavedViewId)
            : applyDefaultSavedView && Request.Query.Count == 0
                ? views.FirstOrDefault(item => item.IsDefault)
                : null;
        if (selected is not null)
        {
            SavedViewId = selected.Id;
            ApplySavedView(selected);
        }
        InitializeExportFiltersForGet();

        var actor = Actor();
        Result = await projectService.SearchProjectsAsync(actor, Query(), cancellationToken);
        var allowedProjectIds = Result.MatchingProjectIds.ToHashSet();
        var financeItems = (await financeService.ListProjectSummariesAsync(cancellationToken))
            .Where(item => allowedProjectIds.Contains(item.ProjectId))
            .ToArray();
        FinanceByProjectId = financeItems.ToDictionary(item => item.ProjectId, item => item.Summary);
        FinanceTotal = SumFinance(financeItems.Select(item => item.Summary));
        var options = await projectService.GetListOptionsAsync(actor, cancellationToken);
        ProjectExportFilters = BuildProjectExportFilters(options);
        Workbench = BuildWorkbench(views, options, selected);
    }

    private void InitializeExportFiltersForGet()
    {
        if (!HttpMethods.IsGet(Request.Method)) return;

        ExportFiltersInitialized = true;
        ExportSearch = Search;
        ExportStages = [.. Stages];
        ExportLegalEntityId = LegalEntityId;
        ExportResponsibleUserId = ResponsibleUserId;
        ExportResponsibleEmployeeId = ResponsibleEmployeeId;
        ExportAffiliationType = AffiliationType;
        ExportMinimumCurrentAmount = MinimumCurrentAmount;
        ExportMaximumCurrentAmount = MaximumCurrentAmount;
    }

    private async Task LoadExportColumnsAsync(CancellationToken cancellationToken)
    {
        var savedViews = await savedViewService.ListAsync(UserId(), ExportViewDefinition, cancellationToken);
        var saved = savedViews.FirstOrDefault(item => string.Equals(item.Name, ExportViewName, StringComparison.Ordinal));
        var requested = ProjectExportColumnKeys;
        if (saved is not null)
        {
            try
            {
                requested = JsonSerializer.Deserialize<string[]>(saved.ColumnJson) ?? [];
            }
            catch (JsonException)
            {
                requested = [];
            }
        }

        ExportColumns = NormalizeExportColumns(requested).ToList();
        if (ExportColumns.Count == 0) ExportColumns = ProjectExportColumnKeys.ToList();
    }

    private async Task SaveExportColumnsAsync(CancellationToken cancellationToken)
    {
        await savedViewService.SaveAsync(
            UserId(),
            new SaveDataViewRequest(
                null,
                ExportViewKey,
                ExportViewName,
                true,
                "{}",
                JsonSerializer.Serialize(ExportColumns),
                null,
                false,
                TableDensity.Standard,
                20),
            ExportViewDefinition,
            cancellationToken);
    }

    private static string[] NormalizeExportColumns(IEnumerable<string>? columns)
    {
        var requested = columns?.ToHashSet(StringComparer.Ordinal) ?? [];
        return ProjectExportColumnKeys.Where(requested.Contains).ToArray();
    }

    private static string NormalizeExportColumnMode(string? mode) =>
        string.Equals(mode, ExportColumnModeTable, StringComparison.OrdinalIgnoreCase)
            ? ExportColumnModeTable
            : ExportColumnModeContent;

    private ProjectListQuery ExportQuery() => new(
        ExportFiltersInitialized ? ExportSearch : Search,
        ExportFiltersInitialized ? ExportStages : Stages,
        ExportFiltersInitialized ? ExportLegalEntityId : LegalEntityId,
        ExportFiltersInitialized ? ExportResponsibleUserId : ResponsibleUserId,
        ExportFiltersInitialized ? ExportMinimumCurrentAmount : MinimumCurrentAmount,
        ExportFiltersInitialized ? ExportMaximumCurrentAmount : MaximumCurrentAmount,
        SortKey,
        SortDescending,
        1,
        100,
        ExportFiltersInitialized ? ExportAffiliationType : AffiliationType,
        false,
        ExportFiltersInitialized ? ExportResponsibleEmployeeId : ResponsibleEmployeeId);

    private IReadOnlyList<DataWorkbenchFilterField> BuildProjectExportFilters(ProjectListOptionsDto options) =>
    [
        new("ExportSearch", "关键词", ExportFiltersInitialized ? ExportSearch : Search, Placeholder: "项目、合同、清单、公司、合作单位、备注"),
        new("ExportStages", "项目阶段", ExportStageValue(), DataWorkbenchFilterKind.Select,
            Enum.GetValues<ProjectStage>().Select(value => new DataWorkbenchFilterOption(((int)value).ToString(System.Globalization.CultureInfo.InvariantCulture), StageLabel(value))).ToArray()),
        new("ExportLegalEntityId", "签约公司", (ExportFiltersInitialized ? ExportLegalEntityId : LegalEntityId)?.ToString(), DataWorkbenchFilterKind.Select,
            options.LegalEntities.Select(item => new DataWorkbenchFilterOption(item.Value, item.Label)).ToArray()),
        new("ExportResponsibleUserId", "负责人账号", ExportFiltersInitialized ? ExportResponsibleUserId : ResponsibleUserId, DataWorkbenchFilterKind.Select,
            options.ResponsibleUsers.Select(item => new DataWorkbenchFilterOption(item.Value, item.Label)).ToArray()),
        new("ExportResponsibleEmployeeId", "员工负责人", (ExportFiltersInitialized ? ExportResponsibleEmployeeId : ResponsibleEmployeeId)?.ToString(), DataWorkbenchFilterKind.Select,
            (options.ResponsibleEmployees ?? []).Select(item => new DataWorkbenchFilterOption(item.Value, item.Label)).ToArray()),
        new("ExportAffiliationType", "合作方式", (ExportFiltersInitialized ? ExportAffiliationType : AffiliationType) is { } affiliation ? ((int)affiliation).ToString(System.Globalization.CultureInfo.InvariantCulture) : null, DataWorkbenchFilterKind.Select,
            [new("1", "自营项目"), new("2", "他方挂靠我方"), new("3", "我方挂靠他方")]),
        new("ExportMinimumCurrentAmount", "最低当前金额", (ExportFiltersInitialized ? ExportMinimumCurrentAmount : MinimumCurrentAmount)?.ToString(System.Globalization.CultureInfo.InvariantCulture), DataWorkbenchFilterKind.Number),
        new("ExportMaximumCurrentAmount", "最高当前金额", (ExportFiltersInitialized ? ExportMaximumCurrentAmount : MaximumCurrentAmount)?.ToString(System.Globalization.CultureInfo.InvariantCulture), DataWorkbenchFilterKind.Number)
    ];

    private string? ExportStageValue()
    {
        var stages = ExportFiltersInitialized ? ExportStages : Stages;
        return stages.Count > 0
            ? ((int)stages[0]).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;
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
        PageSize,
        AffiliationType,
        false,
        ResponsibleEmployeeId);

    private static int NormalizePageSize(int pageSize) => pageSize is 20 or 50 or 100 ? pageSize : 20;

    private ProjectListActor Actor()
    {
        var canAccessAll = User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator) || User.IsInRole(SystemRoles.Finance) || User.IsInRole(SystemRoles.QueryOnly) || User.IsInRole(SystemRoles.EquipmentManager);
        return new ProjectListActor(UserId(), canAccessAll);
    }

    private ProjectWorkbookActor WorkbookActor() =>
        new(UserId(), User.FindAll(ClaimTypes.Role).Select(item => item.Value).Distinct(StringComparer.Ordinal).ToArray());

    private DataWorkbenchViewModel BuildWorkbench(IReadOnlyList<SavedDataViewDto> views, ProjectListOptionsDto options, SavedDataViewDto? selected)
    {
        var filters = new List<DataWorkbenchFilterField>
        {
            new("Search", "关键词", Search, Placeholder: "支持本模块全部信息：项目、合同、清单、公司、合作单位、备注"),
            new("Stages", "项目阶段", Stages.Count > 0 ? ((int)Stages[0]).ToString(System.Globalization.CultureInfo.InvariantCulture) : null, DataWorkbenchFilterKind.Select,
                Enum.GetValues<ProjectStage>().Select(value => new DataWorkbenchFilterOption(((int)value).ToString(System.Globalization.CultureInfo.InvariantCulture), StageLabel(value))).ToArray()),
            new("LegalEntityId", "签约公司", LegalEntityId?.ToString(), DataWorkbenchFilterKind.Select,
                options.LegalEntities.Select(item => new DataWorkbenchFilterOption(item.Value, item.Label)).ToArray()),
            new("ResponsibleEmployeeId", "项目负责人", ResponsibleEmployeeId?.ToString(), DataWorkbenchFilterKind.Select,
                (options.ResponsibleEmployees ?? []).Select(item => new DataWorkbenchFilterOption(item.Value, item.Label)).ToArray()),
            new("AffiliationType", "项目合作方式", AffiliationType.HasValue ? ((int)AffiliationType.Value).ToString(System.Globalization.CultureInfo.InvariantCulture) : null, DataWorkbenchFilterKind.Select,
                [new("1", "自营项目"), new("2", "他方挂靠我方"), new("3", "我方挂靠他方")]),
            new("MinimumCurrentAmount", "最低当前金额", MinimumCurrentAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture), DataWorkbenchFilterKind.Number),
            new("MaximumCurrentAmount", "最高当前金额", MaximumCurrentAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture), DataWorkbenchFilterKind.Number)
        };
        var chips = new List<DataWorkbenchFilterChip>();
        if (!string.IsNullOrWhiteSpace(Search)) chips.Add(new("Search", "关键词", Search));
        if (Stages.Count > 0) chips.Add(new("Stages", "阶段", string.Join("、", Stages.Select(StageLabel))));
        if (LegalEntityId.HasValue) chips.Add(new("LegalEntityId", "签约公司", options.LegalEntities.FirstOrDefault(item => item.Value == LegalEntityId.Value.ToString())?.Label ?? LegalEntityId.Value.ToString()));
        if (ResponsibleEmployeeId.HasValue) chips.Add(new("ResponsibleEmployeeId", "负责人", (options.ResponsibleEmployees ?? []).FirstOrDefault(item => item.Value == ResponsibleEmployeeId.Value.ToString())?.Label ?? ResponsibleEmployeeId.Value.ToString()));
        if (AffiliationType.HasValue) chips.Add(new("AffiliationType", "合作方式", AffiliationTypeLabel(AffiliationType.Value)));
        if (MinimumCurrentAmount.HasValue) chips.Add(new("MinimumCurrentAmount", "最低金额", MinimumCurrentAmount.Value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture)));
        if (MaximumCurrentAmount.HasValue) chips.Add(new("MaximumCurrentAmount", "最高金额", MaximumCurrentAmount.Value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture)));

        return new DataWorkbenchViewModel(
            "projects",
            "projects-table",
            [
                new("serial_number", "序号", true, true),
                new("project_number", "项目编号", true, false),
                new("project_name", "项目名称"),
                new("stage", "阶段"),
                new("contract_signing_status", "合同签订"),
                new("affiliation_type", "合作方式"),
                new("parent_project", "上级项目"),
                new("general_contractor", "总包单位"),
                new("general_contractor_contact", "总包联系人 / 电话"),
                new("responsible_user", "项目负责人"),
                new("department", "部门"),
                new("branch", "分支机构"),
                new("legal_entities", "签约公司"),
                new("actual_start_date", "实际开始日期"),
                new("actual_completion_date", "实际完工日期"),
                new("contract_amount", "合同金额"),
                new("estimated_amount", "预计金额"),
                new("settled_amount", "已结算金额"),
                new("current_project_amount", "当前工程金额"),
                new("settlement_status", "结算状态"),
                new("contract_count", "合同数量"),
                new("line_item_count", "清单项数量"),
                new("collection_progress", "收款（应/已/未）"),
                new("payment_progress", "付款（应/已/未）"),
                new("invoice_progress", "开票（已/未）"),
                new("notes", "备注摘要"),
                new("actions", "操作", false, false)
            ],
            filters,
            chips,
            views,
            selected?.RowDensity ?? TableDensity.Standard,
            PageSize,
            SortKey,
            SortDescending,
            selected?.Id,
            false,
            InlineFilters: [filters[0]],
            ToolbarActionsPartial: CanExportWorkbook ? "_ProjectWorkbookExport" : null,
            ToolbarActionsModel: CanExportWorkbook ? this : null,
            SortOptions:
            [
                new("ProjectNumber", "最新项目在前", true),
                new("ProjectNumber", "最早项目在前", false),
                new("Name", "项目名称：升序", false),
                new("Name", "项目名称：降序", true),
                new("Stage", "项目阶段：升序", false),
                new("Stage", "项目阶段：降序", true),
                new("ContractAmount", "合同金额：高到低", true),
                new("ContractAmount", "合同金额：低到高", false),
                new("CurrentAmount", "当前金额：高到低", true),
                new("CurrentAmount", "当前金额：低到高", false),
                new("SettlementStatus", "结算状态：降序", true),
                new("SettlementStatus", "结算状态：升序", false)
            ],
            DefaultSortKey: "ProjectNumber",
            DefaultSortDescending: true,
            SortOnServer: true);
    }

    private void ApplySavedView(SavedDataViewDto view)
    {
        var filters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(view.FilterJson) ?? [];
        Search = ReadString(filters, "Search") ?? Search;
        if (TryReadStage(ReadString(filters, "Stages"), out var parsedStage)) Stages = [parsedStage];
        if (Guid.TryParse(ReadString(filters, "LegalEntityId"), out var legalEntityId)) LegalEntityId = legalEntityId;
        ResponsibleUserId = ReadString(filters, "ResponsibleUserId") ?? ResponsibleUserId;
        if (Guid.TryParse(ReadString(filters, "ResponsibleEmployeeId"), out var responsibleEmployeeId)) ResponsibleEmployeeId = responsibleEmployeeId;
        if (int.TryParse(ReadString(filters, "AffiliationType"), out var affiliation) && Enum.IsDefined(typeof(ProjectAffiliationType), affiliation)) AffiliationType = (ProjectAffiliationType)affiliation;
        if (decimal.TryParse(ReadString(filters, "MinimumCurrentAmount"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var minimum)) MinimumCurrentAmount = minimum;
        if (decimal.TryParse(ReadString(filters, "MaximumCurrentAmount"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var maximum)) MaximumCurrentAmount = maximum;
        if (!string.IsNullOrWhiteSpace(view.SortKey))
        {
            SortKey = view.SortKey;
            SortDescending = view.SortDescending;
        }
        PageSize = view.PageSize;
    }

    private static string? ReadString(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value) ? value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().FirstOrDefault().ToString() : value.ToString() : null;

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("当前用户没有标识。");

    private static string StageLabel(ProjectStage stage) => stage.ToChinese();

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

    private static string AffiliationTypeLabel(ProjectAffiliationType type) => type switch
    {
        ProjectAffiliationType.ExternalPartyAttachedToUs => "他方挂靠我方",
        ProjectAffiliationType.WeAttachedToExternalParty => "我方挂靠他方",
        _ => "自营项目"
    };

    private static FinanceProjectSummaryDto SumFinance(IEnumerable<FinanceProjectSummaryDto> summaries)
    {
        var items = summaries.ToArray();
        return new FinanceProjectSummaryDto(
            Guid.Empty,
            items.Sum(item => item.ReceivableAmount),
            items.Sum(item => item.CollectedAmount),
            items.Sum(item => item.UncollectedAmount),
            items.Sum(item => item.PayableAmount),
            items.Sum(item => item.PaidAmount),
            items.Sum(item => item.DeductionAmount),
            items.Sum(item => item.UnpaidAmount),
            items.Sum(item => item.OutputInvoiceAmount),
            items.Sum(item => item.UninvoicedAmount),
            items.Sum(item => item.InputInvoiceAmount),
            items.Any(item => item.HasCollectionRisk),
            items.Any(item => item.HasPaymentRisk));
    }

    private static FinanceProjectSummaryDto EmptyFinanceSummary() =>
        new(Guid.Empty, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, false, false);
}
