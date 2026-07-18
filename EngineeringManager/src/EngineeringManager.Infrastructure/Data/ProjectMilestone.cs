namespace EngineeringManager.Infrastructure.Data;

public sealed class ProjectMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateOnly? PlannedDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}
