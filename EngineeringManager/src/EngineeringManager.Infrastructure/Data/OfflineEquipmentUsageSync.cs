namespace EngineeringManager.Infrastructure.Data;

public sealed class OfflineEquipmentUsageSync
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public Guid ClientDraftId { get; set; }
    public Guid LastOperationId { get; set; }
    public Guid EquipmentProjectUsageId { get; set; }
    public EquipmentProjectUsage EquipmentProjectUsage { get; set; } = null!;
    public Guid LastServerVersion { get; set; }
    public string Status { get; set; } = "Synced";
    public string? LastError { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<OfflineEquipmentAttachmentSync> Attachments { get; set; } = [];
}
