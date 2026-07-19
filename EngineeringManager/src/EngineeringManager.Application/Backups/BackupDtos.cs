using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Application.Backups;

public enum BackupKind { Settings = 1, Full = 2 }
public enum BackupScheduleMode { Disabled = 0, Interval = 1, FixedTime = 2 }
public enum BackupTargetStatus { NotConfigured = 0, Pending = 1, Succeeded = 2, Failed = 3 }

public sealed record BackupTaskDto(
    Guid Id,
    string RequestedByUserId,
    DataExchangeTaskStatus Status,
    string? DatabaseBackupPath,
    string? AttachmentArchivePath,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    BackupKind Kind = BackupKind.Full,
    string? PackagePath = null,
    string? Sha256 = null,
    BackupTargetStatus LocalStatus = BackupTargetStatus.Succeeded,
    BackupTargetStatus NasStatus = BackupTargetStatus.NotConfigured,
    bool IsRetained = false);

public sealed record BackupScheduleDto(
    Guid Id,
    BackupKind Kind,
    bool Enabled,
    BackupScheduleMode Mode,
    int? IntervalMinutes,
    TimeOnly? FixedTime,
    string TimeZoneId,
    string? LocalTargetDirectory,
    string? NasTargetDirectory,
    int LocalRetentionCount,
    int NasRetentionCount,
    bool AlertOnFailure,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt);

public sealed record SaveBackupScheduleRequest(
    BackupKind Kind,
    bool Enabled,
    BackupScheduleMode Mode,
    int? IntervalMinutes,
    TimeOnly? FixedTime,
    string TimeZoneId,
    string? LocalTargetDirectory,
    string? NasTargetDirectory,
    int LocalRetentionCount,
    int NasRetentionCount,
    bool AlertOnFailure);

public sealed record SettingsRestorePreviewDto(
    string FormatVersion,
    IReadOnlyList<string> Categories,
    int SettingCount,
    int UserCount);
