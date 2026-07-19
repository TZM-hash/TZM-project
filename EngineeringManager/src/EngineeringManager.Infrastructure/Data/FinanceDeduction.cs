using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceDeduction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SettlementId { get; set; }
    public FinanceSettlement Settlement { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public decimal Amount { get; set; }
    public bool ReduceInvoiceAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LedgerSourceType SourceType { get; set; } = LedgerSourceType.CentralLedger;
    public Guid? SourceId { get; set; }
    public LedgerRecordStatus Status { get; set; } = LedgerRecordStatus.Active;
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
