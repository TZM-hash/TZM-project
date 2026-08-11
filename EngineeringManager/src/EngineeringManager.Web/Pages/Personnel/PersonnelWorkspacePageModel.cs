using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.Personnel;
using EngineeringManager.Application.Settings;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Workbenches;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Personnel;

public abstract class PersonnelWorkspacePageModel : PageModel
{
    private const string ExportColumnModeContent = "content";
    private const string ExportColumnModeTable = "table";

    protected PersonnelWorkspacePageModel(
        IPersonnelService personnelService,
        IBusinessYearService? businessYearService,
        IEmployeeAnnualLedgerService? annualLedgerService,
        ISavedDataViewService? savedViewService,
        IPersonnelWorkbookService? personnelWorkbookService,
        PersonnelScope scope,
        string pageKey,
        string tableId,
        string scopeLabel)
    {
        PersonnelService = personnelService;
        BusinessYearService = businessYearService;
        AnnualLedgerService = annualLedgerService;
        SavedViewService = savedViewService;
        PersonnelWorkbookService = personnelWorkbookService;
        Scope = scope;
        PageKey = pageKey;
        TableId = tableId;
        ScopeLabel = scopeLabel;
    }

    protected IPersonnelService PersonnelService { get; }
    protected IBusinessYearService? BusinessYearService { get; }
    protected IEmployeeAnnualLedgerService? AnnualLedgerService { get; }
    protected ISavedDataViewService? SavedViewService { get; }
    protected IPersonnelWorkbookService? PersonnelWorkbookService { get; }
    protected PersonnelScope Scope { get; }
    protected string PageKey { get; }
    protected string TableId { get; }
    protected string ScopeLabel { get; }

    public IReadOnlyList<PersonnelListItemDto> Personnel { get; protected set; } = [];
    public PersonnelOptionSetDto Options { get; protected set; } = new([], [], [], [], []);
    public IReadOnlyDictionary<Guid, EmployeeAnnualLedgerSummary> AnnualSummaries { get; protected set; } = new Dictionary<Guid, EmployeeAnnualLedgerSummary>();
    public IReadOnlyDictionary<Guid, decimal> PenaltyAmounts { get; protected set; } = new Dictionary<Guid, decimal>();
    public IReadOnlyList<BusinessYearDto> BusinessYears { get; protected set; } = [];
    public Guid? CurrentBusinessYearId { get; protected set; }
    public DataWorkbenchViewModel Workbench { get; protected set; } = null!;
    public IReadOnlyList<DataWorkbenchColumn> PersonnelExportColumns =>
        (PersonnelWorkbookService?.GetColumns() ?? [])
        .Select(column => new DataWorkbenchColumn(column.Key, column.Label))
        .ToArray();
    public bool CanExportWorkbook => PersonnelWorkbookService is not null;
    public bool UsesTableExportColumns => string.Equals(NormalizeExportColumnMode(ExportColumnMode), ExportColumnModeTable, StringComparison.Ordinal);
    public bool UsesContentExportColumns => !UsesTableExportColumns;
    public bool CanViewSensitive => User.IsInRole(SystemRoles.SystemAdministrator)
        || User.IsInRole(SystemRoles.ApplicationAdministrator)
        || User.IsInRole(SystemRoles.Finance);

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessPartnerId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CrewBusinessPartnerId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? DepartmentId { get; set; }
    [BindProperty(SupportsGet = true)] public EmployeeType? InternalType { get; set; }
    [BindProperty(SupportsGet = true)] public ExternalPersonnelType? ExternalType { get; set; }
    [BindProperty(SupportsGet = true)] public bool? IsActive { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? AsOf { get; set; }
    [BindProperty(SupportsGet = true)] public string SortKey { get; set; } = "person_number";
    [BindProperty(SupportsGet = true)] public bool SortDescending { get; set; } = true;
    [BindProperty(SupportsGet = true)] public Guid? SavedViewId { get; set; }
    [BindProperty] public SavedDataViewInput SavedView { get; set; } = new();
    [BindProperty] public List<Guid> SelectedPersonnelIds { get; set; } = [];
    [BindProperty] public bool SelectAllMatching { get; set; }
    [BindProperty] public List<string> ExportColumns { get; set; } = [];
    [BindProperty] public List<string> TableExportColumns { get; set; } = [];
    [BindProperty] public string ExportColumnMode { get; set; } = ExportColumnModeContent;

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    protected abstract PersonnelListQuery BuildQuery();

    protected abstract string PersonnelTypeLabel(PersonnelListItemDto item);

    protected async Task LoadAsync(
        CancellationToken cancellationToken,
        bool loadExportColumns = true,
        bool applyDefaultSavedView = true)
    {
        Options = await PersonnelService.GetOptionsAsync(cancellationToken);
        var views = SavedViewService is null
            ? (IReadOnlyList<SavedDataViewDto>)[]
            : await SavedViewService.ListAsync(UserId(), CreateViewDefinition(), cancellationToken);
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

        if (CanExportWorkbook && loadExportColumns)
        {
            await LoadExportColumnsAsync(cancellationToken);
        }

        Personnel = await PersonnelService.ListAsync(BuildQuery(), CanViewSensitive, cancellationToken);
        await LoadAnnualStateAsync(Personnel, cancellationToken);
        Workbench = BuildWorkbench(views, selected);
    }

    protected async Task<IActionResult> SaveViewAsync(CancellationToken cancellationToken)
    {
        if (SavedViewService is null) return Forbid();

        var filterJson = JsonSerializer.Serialize(CurrentFilterValues());
        var saved = await SavedViewService.SaveAsync(
            UserId(),
            new SaveDataViewRequest(
                SavedView.Id,
                PageKey,
                SavedView.Name,
                SavedView.IsDefault,
                filterJson,
                SavedView.ColumnJson,
                SavedView.SortKey,
                SavedView.SortDescending,
                SavedView.RowDensity,
                20),
            CreateViewDefinition(),
            cancellationToken);
        return RedirectToPage(new { SavedViewId = saved.Id });
    }

    protected async Task<IActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        if (PersonnelWorkbookService is null) return Forbid();

        if (SelectedPersonnelIds.Count > 0) SelectAllMatching = false;
        var matching = await PersonnelService.ListAsync(BuildQuery(), CanViewSensitive, cancellationToken);
        var selected = SelectAllMatching
            ? matching
            : matching.Where(item => SelectedPersonnelIds.Contains(item.Id)).ToArray();
        if (selected.Count == 0)
        {
            ModelState.AddModelError(string.Empty, $"请至少勾选一个{ScopeLabel}，或选择导出当前筛选命中的全部人员。");
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
        var rows = await CreateWorkbookRowsAsync(selected, cancellationToken);
        var file = await PersonnelWorkbookService.ExportAsync(
            new PersonnelWorkbookExportRequest(rows, ExportColumns),
            cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    protected DataWorkbenchViewModel BuildWorkbench(
        IReadOnlyList<SavedDataViewDto> views,
        SavedDataViewDto? selected)
    {
        var columns = DataWorkbenchPresets.PersonnelColumns;
        var exportModel = CanExportWorkbook
            ? new PersonnelWorkbookExportViewModel(
                $"{TableId}-export-form",
                ScopeLabel,
                CurrentQueryParameters(),
                columns,
                PersonnelExportColumns,
                ExportColumns,
                NormalizeExportColumnMode(ExportColumnMode))
            : null;

        return new DataWorkbenchViewModel(
            PageKey,
            TableId,
            columns,
            [],
            [],
            views,
            selected?.RowDensity ?? TableDensity.Standard,
            20,
            SortKey,
            SortDescending,
            selected?.Id,
            CanExport: false,
            CanSaveViews: SavedViewService is not null,
            CanChangePageSize: true,
            ToolbarActionsPartial: CanExportWorkbook ? "_PersonnelWorkbookExport" : null,
            ToolbarActionsModel: exportModel,
            SortOptions:
            [
                new("person_number", "人员编号：最新在前", true),
                new("person_number", "人员编号：最早在前", false),
                new("name", "姓名：升序", false),
                new("name", "姓名：降序", true),
                new("current_year_new_payable", "本年应发：高到低", true),
                new("current_year_new_payable", "本年应发：低到高", false),
                new("current_balance", "当前余额：高到低", true),
                new("current_balance", "当前余额：低到高", false)
            ],
            DefaultSortKey: "person_number",
            DefaultSortDescending: true,
            SortOnServer: false,
            PreservedQueryParameters: CurrentQueryParameters(),
            ClearFiltersUrl: Request.Path);
    }

    protected async Task LoadAnnualStateAsync(
        IReadOnlyList<PersonnelListItemDto> personnel,
        CancellationToken cancellationToken)
    {
        AnnualSummaries = new Dictionary<Guid, EmployeeAnnualLedgerSummary>();
        PenaltyAmounts = new Dictionary<Guid, decimal>();
        BusinessYears = [];
        CurrentBusinessYearId = null;
        if (BusinessYearService is null || AnnualLedgerService is null) return;

        BusinessYears = await BusinessYearService.ListAsync(cancellationToken);
        var current = await BusinessYearService.GetByDateAsync(
            DateOnly.FromDateTime(DateTime.Today),
            cancellationToken)
            ?? (BusinessYears.Count > 0 ? BusinessYears[0] : null);
        if (current is null) return;

        CurrentBusinessYearId = current.Id;
        var summaries = new Dictionary<Guid, EmployeeAnnualLedgerSummary>();
        var penalties = new Dictionary<Guid, decimal>();
        foreach (var item in personnel.Where(item => item.EmployeeId.HasValue))
        {
            var employeeId = item.EmployeeId!.Value;
            var ledger = await AnnualLedgerService.GetAnnualLedgerAsync(employeeId, current.Id, cancellationToken);
            summaries[employeeId] = ledger.Summary;
            var wageEntries = await AnnualLedgerService.GetWageEntriesAsync(employeeId, current.Id, cancellationToken);
            penalties[employeeId] = Math.Abs(wageEntries
                .Where(entry => entry.EntryType == EmployeeWageEntryType.Penalty)
                .Sum(entry => entry.FinalAmount));
        }

        AnnualSummaries = summaries;
        PenaltyAmounts = penalties;
    }

    private async Task<IReadOnlyList<PersonnelWorkbookRow>> CreateWorkbookRowsAsync(
        IReadOnlyList<PersonnelListItemDto> personnel,
        CancellationToken cancellationToken)
    {
        await LoadAnnualStateAsync(personnel, cancellationToken);
        return personnel.Select(ToWorkbookRow).ToArray();
    }

    private PersonnelWorkbookRow ToWorkbookRow(PersonnelListItemDto item)
    {
        var affiliation = item.CurrentAffiliation;
        var summary = item.EmployeeId.HasValue
            ? AnnualSummaries.GetValueOrDefault(item.EmployeeId.Value)
            : null;
        var organization = affiliation?.LegalEntityName ?? affiliation?.BusinessPartnerName;
        return new PersonnelWorkbookRow(
            item.Id,
            item.PersonNumber,
            item.Name,
            item.Phone,
            PersonnelTypeLabel(item),
            affiliation?.PositionTitle,
            organization,
            affiliation?.OrganizationUnitName,
            affiliation?.ProjectName,
            affiliation?.CrewBusinessPartnerName,
            item.IsActive,
            summary,
            summary is null ? null : PenaltyAmounts.GetValueOrDefault(item.EmployeeId!.Value));
    }

    private async Task LoadExportColumnsAsync(CancellationToken cancellationToken)
    {
        var keys = PersonnelExportColumns.Select(item => item.Key).ToArray();
        if (SavedViewService is not null)
        {
            var savedViews = await SavedViewService.ListAsync(UserId(), CreateExportViewDefinition(), cancellationToken);
            var saved = savedViews.FirstOrDefault(item => string.Equals(item.Name, $"{ScopeLabel}导出列", StringComparison.Ordinal));
            if (saved is not null)
            {
                try
                {
                    keys = JsonSerializer.Deserialize<string[]>(saved.ColumnJson) ?? [];
                }
                catch (JsonException)
                {
                    keys = [];
                }
            }
        }

        ExportColumns = NormalizeExportColumns(keys).ToList();
        if (ExportColumns.Count == 0) ExportColumns = PersonnelExportColumns.Select(item => item.Key).ToList();
    }

    private async Task SaveExportColumnsAsync(CancellationToken cancellationToken)
    {
        if (SavedViewService is null) return;
        await SavedViewService.SaveAsync(
            UserId(),
            new SaveDataViewRequest(
                null,
                $"{PageKey}-export",
                $"{ScopeLabel}导出列",
                true,
                "{}",
                JsonSerializer.Serialize(ExportColumns),
                null,
                false,
                TableDensity.Standard,
                20),
            CreateExportViewDefinition(),
            cancellationToken);
    }

    private string[] NormalizeExportColumns(IEnumerable<string>? columns)
    {
        var requested = columns?.ToHashSet(StringComparer.Ordinal) ?? [];
        return PersonnelExportColumns
            .Select(item => item.Key)
            .Where(requested.Contains)
            .ToArray();
    }

    private static string NormalizeExportColumnMode(string? mode) =>
        string.Equals(mode, ExportColumnModeTable, StringComparison.OrdinalIgnoreCase)
            ? ExportColumnModeTable
            : ExportColumnModeContent;

    private DataViewDefinition CreateViewDefinition() => new(
        PageKey,
        new HashSet<string>([
            nameof(Search),
            nameof(LegalEntityId),
            nameof(BusinessPartnerId),
            nameof(CrewBusinessPartnerId),
            nameof(DepartmentId),
            nameof(InternalType),
            nameof(ExternalType),
            nameof(IsActive),
            nameof(AsOf)
        ], StringComparer.Ordinal),
        new HashSet<string>(DataWorkbenchPresets.PersonnelColumns.Select(item => item.Key), StringComparer.Ordinal),
        new HashSet<string>(["person_number", "name", "current_year_new_payable", "current_balance"], StringComparer.Ordinal));

    private DataViewDefinition CreateExportViewDefinition() => new(
        $"{PageKey}-export",
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(PersonnelExportColumns.Select(item => item.Key), StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    private void ApplySavedView(SavedDataViewDto view)
    {
        try
        {
            var filters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(view.FilterJson) ?? [];
            Search = ReadString(filters, nameof(Search)) ?? Search;
            if (Guid.TryParse(ReadString(filters, nameof(LegalEntityId)), out var legalEntityId)) LegalEntityId = legalEntityId;
            if (Guid.TryParse(ReadString(filters, nameof(BusinessPartnerId)), out var businessPartnerId)) BusinessPartnerId = businessPartnerId;
            if (Guid.TryParse(ReadString(filters, nameof(CrewBusinessPartnerId)), out var crewId)) CrewBusinessPartnerId = crewId;
            if (Guid.TryParse(ReadString(filters, nameof(DepartmentId)), out var departmentId)) DepartmentId = departmentId;
            if (Enum.TryParse(ReadString(filters, nameof(InternalType)), true, out EmployeeType internalType)) InternalType = internalType;
            if (Enum.TryParse(ReadString(filters, nameof(ExternalType)), true, out ExternalPersonnelType externalType)) ExternalType = externalType;
            if (bool.TryParse(ReadString(filters, nameof(IsActive)), out var isActive)) IsActive = isActive;
            if (DateOnly.TryParse(ReadString(filters, nameof(AsOf)), CultureInfo.InvariantCulture, DateTimeStyles.None, out var asOf)) AsOf = asOf;
        }
        catch (JsonException)
        {
            // Invalid personal filter JSON is ignored; the page keeps its current query.
        }

        if (!string.IsNullOrWhiteSpace(view.SortKey))
        {
            SortKey = view.SortKey;
            SortDescending = view.SortDescending;
        }
    }

    private static string? ReadString(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value)
            ? value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().FirstOrDefault().ToString()
                : value.ToString()
            : null;

    private Dictionary<string, string?> CurrentFilterValues()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        Add(values, nameof(Search), Search);
        Add(values, nameof(LegalEntityId), LegalEntityId?.ToString());
        Add(values, nameof(BusinessPartnerId), BusinessPartnerId?.ToString());
        Add(values, nameof(CrewBusinessPartnerId), CrewBusinessPartnerId?.ToString());
        Add(values, nameof(DepartmentId), DepartmentId?.ToString());
        Add(values, nameof(InternalType), InternalType?.ToString());
        Add(values, nameof(ExternalType), ExternalType?.ToString());
        Add(values, nameof(IsActive), IsActive?.ToString().ToLowerInvariant());
        Add(values, nameof(AsOf), AsOf?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return values;
    }

    private Dictionary<string, string?> CurrentQueryParameters()
    {
        var values = CurrentFilterValues();
        Add(values, nameof(SortKey), SortKey);
        Add(values, nameof(SortDescending), SortDescending.ToString().ToLowerInvariant());
        return values;
    }

    private static void Add(Dictionary<string, string?> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) values[key] = value;
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("当前用户没有标识。");
}
