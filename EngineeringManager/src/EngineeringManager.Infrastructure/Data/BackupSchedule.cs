using EngineeringManager.Application.Backups;

namespace EngineeringManager.Infrastructure.Data;

public sealed class BackupSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public BackupKind Kind { get; set; }
    public bool Enabled { get; set; }
    public BackupScheduleMode Mode { get; set; }
    public int? IntervalMinutes { get; set; }
    public TimeOnly? FixedTime { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Shanghai";
    public string? LocalTargetDirectory { get; set; }
    public string? NasTargetDirectory { get; set; }
    public int LocalRetentionCount { get; set; } = 10;
    public int NasRetentionCount { get; set; } = 10;
    public bool AlertOnFailure { get; set; } = true;
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
