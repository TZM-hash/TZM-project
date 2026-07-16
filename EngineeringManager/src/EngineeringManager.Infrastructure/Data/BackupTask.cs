using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Infrastructure.Data;

public sealed class BackupTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestedByUserId { get; set; } = string.Empty;
    public DataExchangeTaskStatus Status { get; set; } = DataExchangeTaskStatus.Pending;
    public string? DatabaseBackupPath { get; set; }
    public string? AttachmentArchivePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
