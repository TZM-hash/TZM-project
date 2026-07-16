using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class AccountTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public FinancialAccount Account { get; set; } = null!;
    public AccountTransactionDirection Direction { get; set; }
    public AccountTransactionSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
