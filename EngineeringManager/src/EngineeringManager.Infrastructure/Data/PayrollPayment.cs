using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class PayrollPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PayrollBatchId { get; set; }
    public PayrollBatch Batch { get; set; } = null!;
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid AccountId { get; set; }
    public FinancialAccount Account { get; set; } = null!;
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public PayrollPayeeType PayeeType { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public Guid? PayeeBusinessPartnerId { get; set; }
    public BusinessPartner? PayeeBusinessPartner { get; set; }
    public string? Notes { get; set; }
    public Guid? AccountTransactionId { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
