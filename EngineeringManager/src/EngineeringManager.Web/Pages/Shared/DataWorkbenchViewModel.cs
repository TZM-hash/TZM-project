using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.Settings;

namespace EngineeringManager.Web.Workbenches;

public enum DataWorkbenchFilterKind
{
    Text = 1,
    Select = 2,
    Date = 3,
    Number = 4
}

public sealed record DataWorkbenchColumn(
    string Key,
    string Label,
    bool IsVisible = true,
    bool IsFixed = false);

public sealed record DataWorkbenchFilterOption(string Value, string Label);

public sealed record DataWorkbenchFilterField(
    string Key,
    string Label,
    string? Value = null,
    DataWorkbenchFilterKind Kind = DataWorkbenchFilterKind.Text,
    IReadOnlyList<DataWorkbenchFilterOption>? Options = null,
    string? Placeholder = null);

public sealed record DataWorkbenchFilterChip(string Key, string Label, string Value);

public sealed record DataWorkbenchViewModel(
    string PageKey,
    string TableId,
    IReadOnlyList<DataWorkbenchColumn> Columns,
    IReadOnlyList<DataWorkbenchFilterField> Filters,
    IReadOnlyList<DataWorkbenchFilterChip> ActiveFilters,
    IReadOnlyList<SavedDataViewDto> SavedViews,
    TableDensity RowDensity = TableDensity.Standard,
    int CurrentPageSize = 20,
    string? CurrentSortKey = null,
    bool SortDescending = false,
    Guid? CurrentSavedViewId = null,
    bool CanExport = false);
