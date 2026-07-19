using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Application.Backups;

namespace EngineeringManager.Infrastructure.Data;

public sealed class BackupTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestedByUserId { get; set; } = string.Empty;
    public BackupKind Kind { get; set; } = BackupKind.Full;
    public DataExchangeTaskStatus Status { get; set; } = DataExchangeTaskStatus.Pending;
    public string? DatabaseBackupPath { get; set; }
    public string? AttachmentArchivePath { get; set; }
    public string? PackagePath { get; set; }
    public string? Sha256 { get; set; }
    public BackupTargetStatus LocalStatus { get; set; } = BackupTargetStatus.Succeeded;
    public BackupTargetStatus NasStatus { get; set; } = BackupTargetStatus.NotConfigured;
    public bool IsRetained { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
