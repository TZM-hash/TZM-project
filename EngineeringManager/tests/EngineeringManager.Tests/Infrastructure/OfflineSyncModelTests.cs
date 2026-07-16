using EngineeringManager.Domain.Offline;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class OfflineSyncModelTests
{
    [Fact]
    public async Task DraftAndAttachmentClientIdsAreUniqueWithinTheirOwner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser { UserName = "offline-user", NormalizedUserName = "OFFLINE-USER", DisplayName = "离线用户" };
        var project = new Project { ProjectNumber = "P-OFF-01", Name = "离线项目", Stage = ProjectStage.UnderConstruction };
        var stageResult = new StageResult
        {
            Project = project,
            Title = "离线草稿",
            ResultType = StageResultType.Progress,
            Status = StageResultStatus.Draft,
            ResultDate = new DateOnly(2026, 7, 16),
            IsOfflineDraft = true,
            SubmittedByUser = user
        };
        var attachment = new Attachment
        {
            Project = project,
            StageResult = stageResult,
            StoredName = "offline-photo.jpg",
            OriginalFileName = "现场照片.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            Category = AttachmentCategory.Photo,
            UploadedByUser = user
        };
        var clientDraftId = Guid.NewGuid();
        var clientAttachmentId = Guid.NewGuid();
        var sync = new OfflineDraftSync
        {
            User = user,
            ClientDraftId = clientDraftId,
            StageResult = stageResult,
            LastServerVersion = stageResult.ConcurrencyStamp,
            Status = OfflineSyncStatus.Synced
        };
        sync.Attachments.Add(new OfflineAttachmentSync
        {
            DraftSync = sync,
            ClientAttachmentId = clientAttachmentId,
            Attachment = attachment
        });
        db.Add(sync);
        await db.SaveChangesAsync();

        db.OfflineDraftSyncs.Add(new OfflineDraftSync
        {
            UserId = user.Id,
            ClientDraftId = clientDraftId,
            StageResultId = stageResult.Id,
            LastServerVersion = stageResult.ConcurrencyStamp,
            Status = OfflineSyncStatus.Synced
        });

        var duplicateDraftSave = () => db.SaveChangesAsync();
        await duplicateDraftSave.Should().ThrowAsync<DbUpdateException>();
        db.ChangeTracker.Clear();

        db.OfflineAttachmentSyncs.Add(new OfflineAttachmentSync
        {
            OfflineDraftSyncId = sync.Id,
            ClientAttachmentId = clientAttachmentId,
            AttachmentId = attachment.Id
        });
        var duplicatePhotoSave = () => db.SaveChangesAsync();
        await duplicatePhotoSave.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void SyncForeignKeysPreserveStageResultAndAttachmentHistory()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var db = new ApplicationDbContext(options);
        var draftEntity = db.Model.FindEntityType(typeof(OfflineDraftSync))!;
        var photoEntity = db.Model.FindEntityType(typeof(OfflineAttachmentSync))!;

        draftEntity.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(StageResult)).DeleteBehavior
            .Should().Be(DeleteBehavior.Restrict);
        photoEntity.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(Attachment)).DeleteBehavior
            .Should().Be(DeleteBehavior.Restrict);
    }
}
