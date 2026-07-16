using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class PayrollCostAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PayrollItemId { get; set; }
    public PayrollItem PayrollItem { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public decimal Amount { get; set; }
}
