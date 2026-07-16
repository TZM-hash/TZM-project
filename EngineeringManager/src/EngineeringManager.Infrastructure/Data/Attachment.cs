using EngineeringManager.Domain.StageResults;

namespace EngineeringManager.Infrastructure.Data;

public sealed class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoredName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public AttachmentCategory Category { get; set; }
    public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public Guid? StageResultId { get; set; }
    public StageResult? StageResult { get; set; }
    public string? UploadedByUserId { get; set; }
    public ApplicationUser? UploadedByUser { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
}
