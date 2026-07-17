using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EmployeeNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public EmployeeType EmployeeType { get; set; }
    public string? Phone { get; set; }
    public string? IdentityNumber { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public DateOnly? HireDate { get; set; }
    public DateOnly? LeaveDate { get; set; }
    public string? PositionTitle { get; set; }
    public Guid? DefaultLegalEntityId { get; set; }
    public LegalEntity? DefaultLegalEntity { get; set; }
    public decimal? DefaultMonthlySalary { get; set; }
    public decimal? DefaultDailyRate { get; set; }
    public decimal? DefaultHourlyRate { get; set; }
    public decimal? DefaultPieceworkRate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<EmployeeAffiliationHistory> AffiliationHistory { get; set; } = [];
    public ICollection<EmployeeCertificate> Certificates { get; set; } = [];
}
