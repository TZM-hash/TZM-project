namespace EngineeringManager.Infrastructure.Data;

public sealed class EquipmentMaintenanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public string? MaintenanceType { get; set; }
    public DateOnly? MaintenanceDate { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public decimal? Amount { get; set; }
    public string? Provider { get; set; }
    public string? Notes { get; set; }
}
