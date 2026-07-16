using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ContractLineItemLegalEntityAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractLineItemId { get; set; }
    public ContractLineItem ContractLineItem { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal? Percentage { get; set; }
}
