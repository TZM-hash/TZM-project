using EngineeringManager.Domain.Offline;

namespace EngineeringManager.Infrastructure.Data;

public sealed class OfflineDraftSync
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public Guid ClientDraftId { get; set; }
    public Guid LastOperationId { get; set; }
    public Guid StageResultId { get; set; }
    public StageResult StageResult { get; set; } = null!;
    public Guid LastServerVersion { get; set; }
    public OfflineSyncStatus Status { get; set; } = OfflineSyncStatus.Synced;
    public string? LastError { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<OfflineAttachmentSync> Attachments { get; set; } = [];
}
