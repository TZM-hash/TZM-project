using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EquipmentOwnershipHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public EquipmentTransferType TransferType { get; set; }
    public DateOnly TransferDate { get; set; }
    public Guid? FromLegalEntityId { get; set; }
    public LegalEntity? FromLegalEntity { get; set; }
    public Guid? ToLegalEntityId { get; set; }
    public LegalEntity? ToLegalEntity { get; set; }
    public string? ExternalRecipientName { get; set; }
    public decimal? TransferAmount { get; set; }
    public string? Notes { get; set; }
}
