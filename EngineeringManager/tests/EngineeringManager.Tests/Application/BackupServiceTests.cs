using EngineeringManager.Application.Backups;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Infrastructure.Backups;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task SuccessfulBackupCreatesDatabaseFileAttachmentArchiveAndTaskRecord()
    {
        await using var fixture = await BackupFixture.CreateAsync(new SuccessfulExecutor());
        Directory.CreateDirectory(fixture.AttachmentRoot);
        await File.WriteAllTextAsync(Path.Combine(fixture.AttachmentRoot, "photo.txt"), "附件内容");

        var result = await fixture.Service.CreateBackupAsync("admin-1", CancellationToken.None);

        result.Status.Should().Be(DataExchangeTaskStatus.Completed);
        File.Exists(result.DatabaseBackupPath).Should().BeTrue();
        File.Exists(result.AttachmentArchivePath).Should().BeTrue();
        (await fixture.Db.BackupTasks.SingleAsync()).Status.Should().Be(DataExchangeTaskStatus.Completed);
    }

    [Fact]
    public async Task FailedDatabaseBackupIsRecordedWithoutFalseSuccess()
    {
        await using var fixture = await BackupFixture.CreateAsync(new FailingExecutor());

        var result = await fixture.Service.CreateBackupAsync("admin-2", CancellationToken.None);

        result.Status.Should().Be(DataExchangeTaskStatus.Failed);
        result.ErrorMessage.Should().Contain("测试数据库备份失败");
        result.CompletedAt.Should().NotBeNull();
    }

    private sealed class SuccessfulExecutor : IDatabaseBackupExecutor
    {
        public async Task ExecuteAsync(string destinationPath, CancellationToken cancellationToken) =>
            await File.WriteAllBytesAsync(destinationPath, [1, 2, 3], cancellationToken);
    }

    private sealed class FailingExecutor : IDatabaseBackupExecutor
    {
        public Task ExecuteAsync(string destinationPath, CancellationToken cancellationToken) => throw new InvalidOperationException("测试数据库备份失败");
    }

    private sealed class BackupFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly string root;

        private BackupFixture(SqliteConnection connection, string root, ApplicationDbContext db, IBackupService service)
        {
            this.connection = connection;
            this.root = root;
            Db = db;
            Service = service;
            AttachmentRoot = Path.Combine(root, "attachments");
        }

        public ApplicationDbContext Db { get; }
        public IBackupService Service { get; }
        public string AttachmentRoot { get; }

        public static async Task<BackupFixture> CreateAsync(IDatabaseBackupExecutor executor)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var root = Path.Combine(Path.GetTempPath(), $"engineering-backup-{Guid.NewGuid():N}");
            var attachments = Path.Combine(root, "attachments");
            var backups = Path.Combine(root, "backups");
            return new BackupFixture(connection, root, db, new BackupService(db, executor, attachments, backups));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
