namespace EngineeringManager.Infrastructure.Data;

public sealed class FinanceCashAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CashEntryId { get; set; }
    public FinanceCashEntry CashEntry { get; set; } = null!;
    public Guid SettlementId { get; set; }
    public FinanceSettlement Settlement { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public Guid? ContractLineItemId { get; set; }
    public ContractLineItem? ContractLineItem { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public Guid? CounterLegalEntityId { get; set; }
    public decimal Amount { get; set; }
    public int AllocationOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
