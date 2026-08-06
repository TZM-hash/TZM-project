using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Personnel;

namespace EngineeringManager.Infrastructure.Data;

public sealed class PersonnelEngagementHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public PersonnelScope Scope { get; set; }
    public EmployeeType? InternalType { get; set; }
    public ExternalPersonnelType? ExternalType { get; set; }
    public Guid? LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? CrewBusinessPartnerId { get; set; }
    public BusinessPartner? CrewBusinessPartner { get; set; }
    public string? PositionTitle { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
    public string? Reason { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
