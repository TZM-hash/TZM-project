using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class PaymentReversalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentEntryId { get; set; }
    public PaymentEntry Payment { get; set; } = null!;
    public Guid AccountId { get; set; }
    public FinancialAccount Account { get; set; } = null!;
    public DateOnly EntryDate { get; set; }
    public decimal Amount { get; set; }
    public FinancialAdjustmentType AdjustmentType { get; set; }
    public string Reason { get; set; } = string.Empty;
}
