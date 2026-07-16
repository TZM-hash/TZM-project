using EngineeringManager.Domain.Equipment;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EquipmentLeaseAgreement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public Guid LessorBusinessPartnerId { get; set; }
    public BusinessPartner LessorBusinessPartner { get; set; } = null!;
    public string? ContractNumber { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public RentMode RentMode { get; set; }
    public MonthlyProrationMode MonthlyProrationMode { get; set; } = MonthlyProrationMode.ThirtyDay;
    public decimal UnitRate { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
