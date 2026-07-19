namespace EngineeringManager.Application.DataExchange;

public interface IProjectWorkbookService
{
    IReadOnlyList<ProjectWorkbookSheetDefinition> GetSheets();

    Task<ExportFileResult> ExportAsync(
        ProjectWorkbookExportRequest request,
        CancellationToken cancellationToken);

    Task<ProjectWorkbookImportPreviewDto> PreviewAsync(
        ProjectWorkbookImportRequest request,
        CancellationToken cancellationToken);

    Task ConfirmAsync(ProjectWorkbookActor actor, Guid batchId, CancellationToken cancellationToken);
}
