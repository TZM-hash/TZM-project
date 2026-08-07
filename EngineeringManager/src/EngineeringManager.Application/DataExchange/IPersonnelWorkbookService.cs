namespace EngineeringManager.Application.DataExchange;

public interface IPersonnelWorkbookService
{
    IReadOnlyList<PersonnelWorkbookColumnDefinition> GetColumns();

    Task<ExportFileResult> ExportAsync(
        PersonnelWorkbookExportRequest request,
        CancellationToken cancellationToken);
}
