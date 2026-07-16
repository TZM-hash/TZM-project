using EngineeringManager.Domain.Equipment;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EquipmentSettlementAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SettlementId { get; set; }
    public EquipmentSettlement Settlement { get; set; } = null!;
    public EquipmentAdjustmentDirection Direction { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}
