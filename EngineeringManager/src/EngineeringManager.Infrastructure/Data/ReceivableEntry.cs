using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ReceivableEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public ReceivableSourceType SourceType { get; set; }
    public DateOnly EntryDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public bool IsVoided { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
