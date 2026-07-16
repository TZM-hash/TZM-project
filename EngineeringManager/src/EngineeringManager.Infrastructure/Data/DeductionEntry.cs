using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class DeductionEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PayableEntryId { get; set; }
    public PayableEntry Payable { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public Guid BusinessPartnerId { get; set; }
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public DateOnly EntryDate { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}
