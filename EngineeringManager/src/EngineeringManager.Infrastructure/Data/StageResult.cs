using EngineeringManager.Domain.StageResults;

namespace EngineeringManager.Infrastructure.Data;

public sealed class StageResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public string Title { get; set; } = string.Empty;
    public StageResultType ResultType { get; set; }
    public StageResultStatus Status { get; set; } = StageResultStatus.Draft;
    public DateOnly ResultDate { get; set; }
    public string? Description { get; set; }
    public QualityResult QualityResult { get; set; } = QualityResult.NotChecked;
    public string? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedByUser { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public bool IsOfflineDraft { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<StageResultLine> Lines { get; set; } = [];
    public ICollection<Attachment> Attachments { get; set; } = [];
}
