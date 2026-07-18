using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ProjectAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public ProjectAssignmentType AssignmentType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}
