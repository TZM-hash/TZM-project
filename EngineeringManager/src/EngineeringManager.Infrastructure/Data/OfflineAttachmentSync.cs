namespace EngineeringManager.Infrastructure.Data;

public sealed class OfflineAttachmentSync
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfflineDraftSyncId { get; set; }
    public OfflineDraftSync DraftSync { get; set; } = null!;
    public Guid ClientAttachmentId { get; set; }
    public Guid AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
