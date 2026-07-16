using EngineeringManager.Domain.Employees;

namespace EngineeringManager.Infrastructure.Data;

public sealed class PayrollItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PayrollBatchId { get; set; }
    public PayrollBatch Batch { get; set; } = null!;
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public PayrollItemType ItemType { get; set; }
    public PayrollItemNature Nature { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<PayrollCostAllocation> CostAllocations { get; set; } = [];
}
