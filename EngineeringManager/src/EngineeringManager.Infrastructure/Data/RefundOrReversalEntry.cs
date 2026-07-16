using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class RefundOrReversalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CollectionEntryId { get; set; }
    public CollectionEntry? Collection { get; set; }
    public Guid? ReceivableEntryId { get; set; }
    public ReceivableEntry? Receivable { get; set; }
    public Guid AccountId { get; set; }
    public FinancialAccount Account { get; set; } = null!;
    public DateOnly EntryDate { get; set; }
    public decimal Amount { get; set; }
    public FinancialAdjustmentType AdjustmentType { get; set; }
    public string Reason { get; set; } = string.Empty;
}
