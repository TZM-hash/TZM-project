using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.DataExchange;

public sealed record ExportRequest(
    ExportDataset Dataset,
    string UserId,
    IReadOnlyList<string> SelectedFields,
    DateOnly? CutoffDate,
    IReadOnlyList<Guid>? ProjectIds = null,
    bool CanViewSensitiveData = true,
    ExportScope Scope = ExportScope.CurrentView,
    ExportPackageFormat PackageFormat = ExportPackageFormat.Workbook,
    bool IncludeAttachments = false);

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

public sealed record ExportModuleRequest(
    IReadOnlyList<ExportDataset> Datasets,
    string UserId,
    IReadOnlyDictionary<ExportDataset, IReadOnlyList<string>> SelectedFields,
    IReadOnlyDictionary<ExportDataset, IReadOnlyList<Guid>>? ProjectIds = null,
    ExportPackageFormat PackageFormat = ExportPackageFormat.Workbook,
    bool IncludeAttachments = false,
    bool CanViewSensitiveData = false);

public sealed record ExportTaskDto(
    Guid Id,
    string UserId,
    IReadOnlyList<ExportDataset> Datasets,
    ExportScope Scope,
    ExportPackageFormat PackageFormat,
    bool IncludeAttachments,
    DataExchangeTaskStatus Status,
    int RowCount,
    string? FileName,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
