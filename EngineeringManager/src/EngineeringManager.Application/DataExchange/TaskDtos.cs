using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.DataExchange;

public sealed record DataExchangeTaskQuery(
    string UserId,
    bool CanManage,
    int Page = 1,
    int PageSize = 20,
    DataExchangeDirection? Direction = null);

public sealed record DataExchangeTaskItemDto(
    Guid Id,
    DataExchangeDirection Direction,
    string UserId,
    IReadOnlyList<ExportDataset> Datasets,
    DataExchangeTaskStatus Status,
    int RowCount,
    int ErrorRowCount,
    string? FileName,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    ExportScope? Scope,
    ExportPackageFormat? PackageFormat,
    ImportMode? ImportMode,
    ImportSourceType? SourceType,
    bool CanDownload,
    bool CanDownloadErrors);

public sealed record DataExchangeTaskPageDto(
    IReadOnlyList<DataExchangeTaskItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
