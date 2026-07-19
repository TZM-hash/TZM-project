using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.DataExchange;

public interface IExportService
{
    IReadOnlyList<ExportFieldDefinition> GetFieldCatalog(ExportDataset dataset);
    Task<ExportFileResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken);
    Task<ExportSelectionDto?> GetLastSelectionAsync(string userId, ExportDataset dataset, CancellationToken cancellationToken);
    Task<ExportTemplateDto> SaveTemplateAsync(SaveExportTemplateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExportTemplateDto>> ListTemplatesAsync(string userId, ExportDataset dataset, CancellationToken cancellationToken);
    Task<ExportFileResult> ExportModulesAsync(ExportModuleRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExportTaskDto>> ListTasksAsync(string userId, CancellationToken cancellationToken);
}
