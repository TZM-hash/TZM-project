using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LedgerScope Scope { get; set; }
    public LedgerDirection Direction { get; set; }
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public Guid? CounterLegalEntityId { get; set; }
    public LegalEntity? CounterLegalEntity { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public Guid? ProjectTaxConfigurationId { get; set; }
    public ProjectTaxConfiguration? ProjectTaxConfiguration { get; set; }
    public string? InvoiceType { get; set; }
    public decimal Amount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TaxRate { get; set; }
    public LedgerRecordStatus Status { get; set; } = LedgerRecordStatus.Active;
    public LedgerSourceType SourceType { get; set; } = LedgerSourceType.CentralLedger;
    public Guid? SourceId { get; set; }
    public string? SourceUrl { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<FinanceInvoiceAllocation> Allocations { get; set; } = [];
}
