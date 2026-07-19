using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceSettlementAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SettlementId { get; set; }
    public FinanceSettlement Settlement { get; set; } = null!;
    public LedgerAdjustmentType AdjustmentType { get; set; }
    public decimal AmountDelta { get; set; }
    public decimal InvoiceAmountDelta { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ActorUserId { get; set; }
    public string? ActorUserName { get; set; }
    public LedgerSourceType SourceType { get; set; } = LedgerSourceType.CentralLedger;
    public Guid? SourceId { get; set; }
    public LedgerRecordStatus Status { get; set; } = LedgerRecordStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
