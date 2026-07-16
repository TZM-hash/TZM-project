using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Infrastructure.Data;

public sealed class Contract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ContractType ContractType { get; set; }
    public ContractAllocationMode AllocationMode { get; set; } = ContractAllocationMode.SingleCompany;
    public string? CounterpartyName { get; set; }
    public DateOnly? SignedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<ContractLegalEntityAllocation> LegalEntityAllocations { get; set; } = [];
    public ICollection<ContractLineItem> LineItems { get; set; } = [];
}
