using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EmployeeOtherPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public EmployeeLedgerEntryType EntryType { get; set; }
    public EmployeeLedgerRecordKind RecordKind { get; set; }
    public Guid? RelatedPayableId { get; set; }
    public EmployeeOtherPayment? RelatedPayable { get; set; }
    public Guid? AccountId { get; set; }
    public FinancialAccount? Account { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public Guid? AttachmentId { get; set; }
    public Attachment? Attachment { get; set; }
    public string? Description { get; set; }
    public Guid? AccountTransactionId { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
