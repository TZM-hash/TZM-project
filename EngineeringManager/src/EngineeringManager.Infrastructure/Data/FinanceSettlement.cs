using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceSettlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LedgerScope Scope { get; set; }
    public LedgerDirection Direction { get; set; }
    public LedgerSettlementState SettlementState { get; set; }
    public LedgerSourceType SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? SourceUrl { get; set; }
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
    public Guid? ContractLineItemId { get; set; }
    public ContractLineItem? ContractLineItem { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? SettlementDate { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal OriginalInvoiceAmount { get; set; }
    public LedgerRecordStatus Status { get; set; } = LedgerRecordStatus.Active;
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<FinanceSettlementAdjustment> Adjustments { get; set; } = [];
    public ICollection<FinanceDeduction> Deductions { get; set; } = [];
    public ICollection<FinanceInvoiceAllocation> InvoiceAllocations { get; set; } = [];
    public ICollection<FinanceCashAllocation> CashAllocations { get; set; } = [];
}
