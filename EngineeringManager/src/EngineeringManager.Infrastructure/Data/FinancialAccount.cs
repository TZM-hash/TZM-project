using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class FinancialAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public Guid? OwnerEmployeeId { get; set; }
    public Employee? OwnerEmployee { get; set; }
    public string? OwnerName { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string? BankName { get; set; }
    public FinancialAccountType AccountType { get; set; }
    public decimal OpeningBalance { get; set; }
    public string? Notes { get; set; }
    public bool IsDefaultCollection { get; set; }
    public bool IsDefaultPayment { get; set; }
    public bool IsDefaultInvoice { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
