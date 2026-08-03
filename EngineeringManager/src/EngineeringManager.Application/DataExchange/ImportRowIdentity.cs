namespace EngineeringManager.Application.DataExchange;

public sealed record ImportRowIdentity(
    string? RecordId,
    string? BusinessKey,
    string? RowVersion,
    string DatasetKey,
    string DatasetVersion,
    string ExportBatchId);
