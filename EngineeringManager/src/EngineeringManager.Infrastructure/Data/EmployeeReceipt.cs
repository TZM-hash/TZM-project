using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EmployeeReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid BusinessYearId { get; set; }
    public BusinessYear BusinessYear { get; set; } = null!;
    public DateOnly ReceiptDate { get; set; }
    public EmployeeReceiptType ReceiptType { get; set; }
    public decimal Amount { get; set; }
    public Guid PaymentLegalEntityId { get; set; }
    public LegalEntity PaymentLegalEntity { get; set; } = null!;
    public Guid AccountId { get; set; }
    public FinancialAccount Account { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; }
    public string ActualRecipientName { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? LaborBusinessPartnerId { get; set; }
    public BusinessPartner? LaborBusinessPartner { get; set; }
    public string? Notes { get; set; }
    public Guid? AccountTransactionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
