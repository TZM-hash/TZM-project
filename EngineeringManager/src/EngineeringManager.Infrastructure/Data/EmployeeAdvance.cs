using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EmployeeAdvance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid LegalEntityId { get; set; }
    public LegalEntity LegalEntity { get; set; } = null!;
    public Guid? AccountId { get; set; }
    public FinancialAccount? Account { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Amount { get; set; }
    public EmployeeAdvanceAction Action { get; set; }
    public string? Description { get; set; }
    public Guid? AccountTransactionId { get; set; }
}
