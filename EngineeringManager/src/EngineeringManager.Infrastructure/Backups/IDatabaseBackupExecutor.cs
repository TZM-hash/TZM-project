namespace EngineeringManager.Infrastructure.Backups;

public interface IDatabaseBackupExecutor
{
    Task ExecuteAsync(string destinationPath, CancellationToken cancellationToken);
}
