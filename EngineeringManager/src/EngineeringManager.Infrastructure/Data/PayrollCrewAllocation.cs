namespace EngineeringManager.Infrastructure.Data;

public sealed class PayrollCrewAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PayrollBatchId { get; set; }
    public PayrollBatch Batch { get; set; } = null!;
    public Guid CrewBusinessPartnerId { get; set; }
    public BusinessPartner CrewBusinessPartner { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public Guid? PayableEntryId { get; set; }
    public PayableEntry? PayableEntry { get; set; }
    public string? Notes { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
