using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ExpensePayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseRecordId { get; set; }
    public ExpenseRecord Expense { get; set; } = null!;
    public Guid AccountId { get; set; }
    public FinancialAccount Account { get; set; } = null!;
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public EmployeeLedgerRecordKind RecordKind { get; set; }
    public string? Notes { get; set; }
    public Guid? AccountTransactionId { get; set; }
}
