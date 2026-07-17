using EngineeringManager.Application.Settings;

namespace EngineeringManager.Application.DataViews;

public sealed record DataViewDefinition(
    string PageKey,
    IReadOnlySet<string> FilterKeys,
    IReadOnlySet<string> ColumnKeys,
    IReadOnlySet<string> SortKeys);

public sealed record SaveDataViewRequest(
    Guid? Id,
    string PageKey,
    string Name,
    bool IsDefault,
    string FilterJson,
    string ColumnJson,
    string? SortKey,
    bool SortDescending,
    TableDensity RowDensity,
    int PageSize);

public sealed record SavedDataViewDto(
    Guid Id,
    string PageKey,
    string Name,
    bool IsDefault,
    string FilterJson,
    string ColumnJson,
    string? SortKey,
    bool SortDescending,
    TableDensity RowDensity,
    int PageSize,
    DateTimeOffset UpdatedAt);
