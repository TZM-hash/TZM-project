using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceCashEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LedgerScope Scope { get; set; }
    public LedgerDirection Direction { get; set; }
    public LedgerCashType CashType { get; set; }
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public Guid? CounterLegalEntityId { get; set; }
    public LegalEntity? CounterLegalEntity { get; set; }
    public Guid? AccountId { get; set; }
    public FinancialAccount? Account { get; set; }
    public Guid? CounterAccountId { get; set; }
    public FinancialAccount? CounterAccount { get; set; }
    public bool IsReversal { get; set; }
    public Guid? ReversesCashEntryId { get; set; }
    public FinanceCashEntry? ReversesCashEntry { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public LedgerRecordStatus Status { get; set; } = LedgerRecordStatus.Active;
    public LedgerSourceType SourceType { get; set; } = LedgerSourceType.CentralLedger;
    public Guid? SourceId { get; set; }
    public string? SourceUrl { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<FinanceCashAllocation> Allocations { get; set; } = [];
}
