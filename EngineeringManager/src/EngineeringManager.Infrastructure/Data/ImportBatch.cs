using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CreatedByUserId { get; set; } = string.Empty;
    public ExportDataset Dataset { get; set; }
    public ImportMode Mode { get; set; } = ImportMode.Mixed;
    public string OriginalFileName { get; set; } = string.Empty;
    public byte[] OriginalContent { get; set; } = [];
    public string MappingJson { get; set; } = "{}";
    public DataExchangeTaskStatus Status { get; set; } = DataExchangeTaskStatus.Pending;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<ImportError> Errors { get; set; } = [];
}
