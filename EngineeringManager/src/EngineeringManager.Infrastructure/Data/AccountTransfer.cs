namespace EngineeringManager.Infrastructure.Data;

public sealed class AccountTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromAccountId { get; set; }
    public FinancialAccount FromAccount { get; set; } = null!;
    public Guid ToAccountId { get; set; }
    public FinancialAccount ToAccount { get; set; } = null!;
    public DateOnly TransferDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public Guid? OutTransactionId { get; set; }
    public Guid? InTransactionId { get; set; }
}
