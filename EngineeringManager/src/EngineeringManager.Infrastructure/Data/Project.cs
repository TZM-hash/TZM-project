using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Infrastructure.Data;

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProjectNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentProjectName { get; set; }
    public string? GeneralContractorName { get; set; }
    public string? GeneralContractorContact { get; set; }
    public string? GeneralContractorPhone { get; set; }
    public string? ResponsibleUserId { get; set; }
    public ApplicationUser? ResponsibleUser { get; set; }
    public Guid? DepartmentId { get; set; }
    public OrganizationUnit? Department { get; set; }
    public Guid? BranchId { get; set; }
    public OrganizationUnit? Branch { get; set; }
    public ProjectStage Stage { get; set; } = ProjectStage.Preliminary;
    public ProjectAffiliationType AffiliationType { get; set; } = ProjectAffiliationType.SelfOperated;
    public ArchiveStatus ArchiveStatus { get; set; } = ArchiveStatus.NotArchived;
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<ProjectAssignment> Assignments { get; set; } = [];
    public ICollection<ProjectLegalEntity> LegalEntities { get; set; } = [];
    public ICollection<ProjectMilestone> Milestones { get; set; } = [];
    public ICollection<Contract> Contracts { get; set; } = [];
    public ICollection<ProjectPartner> Partners { get; set; } = [];
    public ICollection<ProjectConstructionRecord> ConstructionRecords { get; set; } = [];
}
