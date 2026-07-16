using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class PayrollBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BatchNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollBatchType BatchType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public string? StageOrMilestoneName { get; set; }
    public PayrollBatchStatus Status { get; set; } = PayrollBatchStatus.Draft;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<PayrollItem> Items { get; set; } = [];
    public ICollection<PayrollPayment> Payments { get; set; } = [];
}
