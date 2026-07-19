using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.DataExchange;

public sealed record ImportPreviewRequest(
    string UserId,
    ExportDataset Dataset,
    string OriginalFileName,
    byte[] Content,
    IReadOnlyDictionary<string, string>? SourceToTargetMapping,
    ImportMode Mode = ImportMode.Mixed,
    bool IncludeAttachments = false);

public sealed record ImportErrorDto(int RowNumber, string ColumnName, string Message, string? RawValue);

public sealed record ImportPreviewDto(
    Guid BatchId,
    ExportDataset Dataset,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    IReadOnlyList<ImportErrorDto> Errors);

public sealed record ImportMappingTemplateDto(
    Guid Id,
    string OwnerUserId,
    string Name,
    ExportDataset Dataset,
    ExportTemplateScope Scope,
    string DatasetVersion,
    IReadOnlyDictionary<string, string> Mapping);

public sealed record SaveImportMappingTemplateRequest(
    string OwnerUserId,
    string Name,
    ExportDataset Dataset,
    ExportTemplateScope Scope,
    string DatasetVersion,
    IReadOnlyDictionary<string, string> Mapping,
    bool CanPublishShared);
