using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.DataExchange;

public interface IImportService
{
    Task<ExportFileResult> GenerateTemplateAsync(ExportDataset dataset, CancellationToken cancellationToken);
    Task<ImportPreviewDto> PreviewAsync(ImportPreviewRequest request, CancellationToken cancellationToken);
    Task ConfirmAsync(Guid batchId, CancellationToken cancellationToken);
}
