using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.DataExchange;

public sealed record ExportRequest(
    ExportDataset Dataset,
    string UserId,
    IReadOnlyList<string> SelectedFields,
    DateOnly? CutoffDate);

public sealed record ExportFileResult(string FileName, string ContentType, byte[] Content);

public sealed record ExportSelectionDto(
    ExportDataset Dataset,
    IReadOnlyList<string> SelectedFields,
    DateOnly? CutoffDate);

public sealed record SaveExportTemplateRequest(
    string OwnerUserId,
    string Name,
    ExportDataset Dataset,
    ExportTemplateScope Scope,
    IReadOnlyList<string> SelectedFields,
    DateOnly? CutoffDate,
    bool CanPublishShared);

public sealed record ExportTemplateDto(
    Guid Id,
    string OwnerUserId,
    string Name,
    ExportDataset Dataset,
    ExportTemplateScope Scope,
    IReadOnlyList<string> SelectedFields,
    DateOnly? CutoffDate);
