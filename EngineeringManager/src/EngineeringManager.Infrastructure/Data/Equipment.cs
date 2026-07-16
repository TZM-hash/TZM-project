using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class Equipment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EquipmentNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Category { get; set; }
    public EquipmentOwnershipType OwnershipType { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Idle;
    public Guid? OwnerLegalEntityId { get; set; }
    public LegalEntity? OwnerLegalEntity { get; set; }
    public Guid? LessorBusinessPartnerId { get; set; }
    public BusinessPartner? LessorBusinessPartner { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public decimal? InternalDailyRate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<EquipmentLeaseAgreement> LeaseAgreements { get; set; } = [];
    public ICollection<EquipmentProjectUsage> ProjectUsages { get; set; } = [];
    public ICollection<EquipmentOwnershipHistory> OwnershipHistory { get; set; } = [];
    public ICollection<EquipmentMaintenanceRecord> MaintenanceRecords { get; set; } = [];
}
