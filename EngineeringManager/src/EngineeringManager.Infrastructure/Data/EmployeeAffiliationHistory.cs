using EngineeringManager.Domain.Organization;

namespace EngineeringManager.Infrastructure.Data;

public sealed class EmployeeAffiliationHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? DepartmentId { get; set; }
    public OrganizationUnit? Department { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? CrewBusinessPartnerId { get; set; }
    public BusinessPartner? CrewBusinessPartner { get; set; }
    public Guid? LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public string? PositionTitle { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
}
