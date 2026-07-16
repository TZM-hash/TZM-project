using EngineeringManager.Domain.Reminders;

namespace EngineeringManager.Application.Reminders;

public sealed record ReminderDto(
    Guid Id,
    string DeduplicationKey,
    ReminderType Type,
    ReminderSeverity Severity,
    ReminderStatus Status,
    string Title,
    string Message,
    string? SourceType,
    string? SourceId,
    DateOnly? DueDate,
    decimal? Amount,
    DateTimeOffset LastOccurredAt);
