using EngineeringManager.Domain.Reminders;

namespace EngineeringManager.Infrastructure.Data;

public sealed class ReminderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeduplicationKey { get; set; } = string.Empty;
    public ReminderType Type { get; set; }
    public ReminderSeverity Severity { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Unread;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? Amount { get; set; }
    public DateTimeOffset FirstOccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastOccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
