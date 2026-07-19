namespace EngineeringManager.Application.Backups;

public interface IBackupService
{
    Task<BackupTaskDto> CreateBackupAsync(string requestedByUserId, CancellationToken cancellationToken);
    Task<BackupTaskDto> CreateBackupAsync(string requestedByUserId, BackupKind kind, CancellationToken cancellationToken);
    Task<IReadOnlyList<BackupTaskDto>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BackupScheduleDto>> ListSchedulesAsync(CancellationToken cancellationToken);
    Task<BackupScheduleDto> SaveScheduleAsync(SaveBackupScheduleRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BackupTaskDto>> RunDueSchedulesAsync(CancellationToken cancellationToken);
    Task<SettingsRestorePreviewDto> PreviewSettingsAsync(byte[] content, CancellationToken cancellationToken);
    Task RestoreSettingsAsync(byte[] content, IReadOnlyCollection<string> categories, string requestedByUserId, CancellationToken cancellationToken);
}
