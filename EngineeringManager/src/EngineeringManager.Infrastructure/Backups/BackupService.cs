using System.IO.Compression;
using EngineeringManager.Application.Backups;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Backups;

public sealed class BackupService(
    ApplicationDbContext db,
    IDatabaseBackupExecutor databaseBackupExecutor,
    string attachmentRoot,
    string backupRoot) : IBackupService
{
    public async Task<BackupTaskDto> CreateBackupAsync(string requestedByUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedByUserId))
        {
            throw new ArgumentException("备份任务必须记录操作用户。", nameof(requestedByUserId));
        }

        Directory.CreateDirectory(attachmentRoot);
        Directory.CreateDirectory(backupRoot);
        var suffix = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        var databasePath = Path.GetFullPath(Path.Combine(backupRoot, $"EngineeringManager_{suffix}.bak"));
        var attachmentPath = Path.GetFullPath(Path.Combine(backupRoot, $"EngineeringManager_Attachments_{suffix}.zip"));
        var task = new BackupTask
        {
            RequestedByUserId = requestedByUserId.Trim(),
            Status = DataExchangeTaskStatus.Running,
            DatabaseBackupPath = databasePath,
            AttachmentArchivePath = attachmentPath,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.BackupTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await databaseBackupExecutor.ExecuteAsync(databasePath, cancellationToken);
            ZipFile.CreateFromDirectory(attachmentRoot, attachmentPath, CompressionLevel.Fastest, includeBaseDirectory: false);
            task.Status = DataExchangeTaskStatus.Completed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            task.Status = DataExchangeTaskStatus.Failed;
            task.ErrorMessage = exception.Message;
        }

        task.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    public async Task<IReadOnlyList<BackupTaskDto>> ListAsync(CancellationToken cancellationToken)
    {
        var tasks = await db.BackupTasks.AsNoTracking().OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return tasks.Select(ToDto).ToArray();
    }

    private static BackupTaskDto ToDto(BackupTask task) =>
        new(task.Id, task.RequestedByUserId, task.Status, task.DatabaseBackupPath, task.AttachmentArchivePath, task.ErrorMessage, task.CreatedAt, task.StartedAt, task.CompletedAt);
}
