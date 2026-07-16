using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.DataExchange;

public sealed record ImportPreviewRequest(
    string UserId,
    ExportDataset Dataset,
    string OriginalFileName,
    byte[] Content,
    IReadOnlyDictionary<string, string>? SourceToTargetMapping);

public sealed record ImportErrorDto(int RowNumber, string ColumnName, string Message, string? RawValue);

public sealed record ImportPreviewDto(
    Guid BatchId,
    ExportDataset Dataset,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    IReadOnlyList<ImportErrorDto> Errors);
