namespace EngineeringManager.Infrastructure.Data;

public sealed class ImportError
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportBatchId { get; set; }
    public ImportBatch Batch { get; set; } = null!;
    public int RowNumber { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RawValue { get; set; }
}
