namespace EngineeringManager.Application.Backups;

public interface IBackupService
{
    Task<BackupTaskDto> CreateBackupAsync(string requestedByUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BackupTaskDto>> ListAsync(CancellationToken cancellationToken);
}
