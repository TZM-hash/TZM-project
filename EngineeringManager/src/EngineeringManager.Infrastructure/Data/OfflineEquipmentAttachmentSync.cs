namespace EngineeringManager.Infrastructure.Data;

public sealed class OfflineEquipmentAttachmentSync
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfflineEquipmentUsageSyncId { get; set; }
    public OfflineEquipmentUsageSync UsageSync { get; set; } = null!;
    public Guid ClientAttachmentId { get; set; }
    public Guid AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}
