namespace EngineeringManager.Application.DataExchange;

public interface IDataExchangeTaskService
{
    Task<DataExchangeTaskPageDto> ListAsync(DataExchangeTaskQuery query, CancellationToken cancellationToken);

    Task<ExportFileResult> DownloadExportAsync(
        string userId,
        bool canManage,
        Guid taskId,
        CancellationToken cancellationToken);

    Task<ExportFileResult> DownloadImportErrorsAsync(
        string userId,
        bool canManage,
        Guid batchId,
        CancellationToken cancellationToken);
}
