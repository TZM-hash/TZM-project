using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.DataExchange;

/// <summary>
/// A row in a standard round-trip workbook.  The technical values are kept
/// separate from the user-editable values so the workbook builder can append
/// and protect control columns consistently for every dataset.
/// </summary>
public sealed record RoundTripWorkbookRow(
    IReadOnlyDictionary<string, object?> Values,
    string? RecordId = null,
    string? BusinessKey = null,
    string? RowVersion = null);

public sealed record RoundTripWorkbookSheet(
    ExportDataset Dataset,
    string WorksheetName,
    IReadOnlyList<ExportFieldDefinition> Fields,
    IReadOnlyList<RoundTripWorkbookRow> Rows,
    string? Description = null);

public sealed record RoundTripWorkbookRequest(
    string ExportBatchId,
    IReadOnlyList<RoundTripWorkbookSheet> Sheets,
    string DatasetVersion = "1",
    string? SourcePage = null,
    DateTimeOffset? ExportedAt = null,
    string WorkbookVersion = "data-exchange/1");
