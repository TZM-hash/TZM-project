using EngineeringManager.Domain.Equipment;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EquipmentWorkPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsageId { get; set; }
    public EquipmentProjectUsage Usage { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public EquipmentPeriodType PeriodType { get; set; }
    public bool IsChargeable { get; set; }
    public string? Notes { get; set; }
}
