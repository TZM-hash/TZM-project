using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ContractLegalEntityAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public decimal? Amount { get; set; }
    public decimal? Percentage { get; set; }
}
