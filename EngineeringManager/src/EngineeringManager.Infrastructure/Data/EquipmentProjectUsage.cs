using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EquipmentProjectUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public Guid? LeaseAgreementId { get; set; }
    public EquipmentLeaseAgreement? LeaseAgreement { get; set; }
    public DateOnly EntryDate { get; set; }
    public DateOnly? ExitDate { get; set; }
    public RentMode RentMode { get; set; }
    public MonthlyProrationMode MonthlyProrationMode { get; set; } = MonthlyProrationMode.ThirtyDay;
    public decimal UnitRate { get; set; }
    public bool SharedUsageOverride { get; set; }
    public string? SharedUsageReason { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<EquipmentWorkPeriod> Periods { get; set; } = [];
    public ICollection<EquipmentAdvancePayment> AdvancePayments { get; set; } = [];
    public EquipmentSettlement? Settlement { get; set; }
}
