using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Infrastructure.Data;

public sealed class DataExchangeTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public DataExchangeDirection Direction { get; set; }
    public string DatasetsJson { get; set; } = "[]";
    public string SelectedFieldsJson { get; set; } = "{}";
    public string FilterJson { get; set; } = "{}";
    public ExportScope Scope { get; set; }
    public ExportPackageFormat PackageFormat { get; set; }
    public bool IncludeAttachments { get; set; }
    public DataExchangeTaskStatus Status { get; set; } = DataExchangeTaskStatus.Pending;
    public int RowCount { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public byte[]? ResultContent { get; set; }
    public string? Sha256 { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
