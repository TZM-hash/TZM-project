namespace EngineeringManager.Infrastructure.Data;

public sealed class AuditLog
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? RelatedProjectId { get; set; }

    public string? Reason { get; set; }

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public string? AttachmentChangesJson { get; set; }

    public string? IpAddress { get; set; }

    public string? RequestId { get; set; }

    public string? UserAgent { get; set; }
}
