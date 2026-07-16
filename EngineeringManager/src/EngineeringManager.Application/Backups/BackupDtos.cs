using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.Backups;

public sealed record BackupTaskDto(
    Guid Id,
    string RequestedByUserId,
    DataExchangeTaskStatus Status,
    string? DatabaseBackupPath,
    string? AttachmentArchivePath,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);
