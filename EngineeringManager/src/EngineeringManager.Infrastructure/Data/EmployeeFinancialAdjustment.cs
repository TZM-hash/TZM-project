using EngineeringManager.Domain.Employees;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EmployeeFinancialAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid BusinessYearId { get; set; }
    public BusinessYear BusinessYear { get; set; } = null!;
    public DateOnly AdjustmentDate { get; set; }
    public decimal Amount { get; set; }
    public EmployeeFinancialAdjustmentType AdjustmentType { get; set; }
    public string Notes { get; set; } = string.Empty;
    public Guid? ReversalOfId { get; set; }
    public EmployeeFinancialAdjustment? ReversalOf { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
