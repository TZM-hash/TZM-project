using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Infrastructure.Data;

public sealed class PayrollPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PayrollBatchId { get; set; }
    public PayrollBatch Batch { get; set; } = null!;
    public PayrollRecipientType RecipientType { get; set; } = PayrollRecipientType.Employee;
    public string? RecipientKey { get; set; }
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid? ConstructionWorkerId { get; set; }
    public ConstructionWorker? ConstructionWorker { get; set; }
    public Guid? TemporaryWorkerId { get; set; }
    public TemporaryWorker? TemporaryWorker { get; set; }
    public Guid? CrewBusinessPartnerId { get; set; }
    public BusinessPartner? CrewBusinessPartner { get; set; }
    public Guid? AccountId { get; set; }
    public FinancialAccount? Account { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public PayrollPayeeType PayeeType { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public Guid? PayeeBusinessPartnerId { get; set; }
    public BusinessPartner? PayeeBusinessPartner { get; set; }
    public string? RecipientNameSnapshot { get; set; }
    public string? IdentityNumberSnapshot { get; set; }
    public string? PhoneSnapshot { get; set; }
    public string? BankAccountSnapshot { get; set; }
    public string? TradeSnapshot { get; set; }
    public string? CrewNameSnapshot { get; set; }
    public string? Notes { get; set; }
    public Guid? AccountTransactionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
