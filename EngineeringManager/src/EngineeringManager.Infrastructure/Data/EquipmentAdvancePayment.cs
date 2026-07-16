using EngineeringManager.Domain.Equipment;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EquipmentAdvancePayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsageId { get; set; }
    public EquipmentProjectUsage Usage { get; set; } = null!;
    public EquipmentAdvancePaymentType PaymentType { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public Guid? PaymentEntryId { get; set; }
    public PaymentEntry? PaymentEntry { get; set; }
    public string? Notes { get; set; }
}
