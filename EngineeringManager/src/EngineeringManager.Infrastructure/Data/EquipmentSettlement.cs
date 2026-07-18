namespace EngineeringManager.Infrastructure.Data;

public sealed class EquipmentSettlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsageId { get; set; }
    public EquipmentProjectUsage Usage { get; set; } = null!;
    public DateOnly SettlementDate { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal OffsetAmount { get; set; }
    public Guid? PayableEntryId { get; set; }
    public PayableEntry? PayableEntry { get; set; }
    public string ModificationReason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? PreviousSnapshotJson { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<EquipmentSettlementAdjustment> Adjustments { get; set; } = [];
}
