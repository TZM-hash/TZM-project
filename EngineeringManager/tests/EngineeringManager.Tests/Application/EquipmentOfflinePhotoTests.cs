using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.EquipmentOffline;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Equipment;
using EngineeringManager.Infrastructure.EquipmentOffline;
using EngineeringManager.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class EquipmentOfflinePhotoTests
{
    [Fact]
    public async Task PhotoSyncIsIndependentlyIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        var user = new ApplicationUser { UserName = "photo-user", NormalizedUserName = "PHOTO-USER", DisplayName = "照片用户" };
        var company = new LegalEntity { Code = "PHOTO-C", Name = "照片公司", ShortName = "照片" };
        var project = new Project { ProjectNumber = "PHOTO-P", Name = "照片项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var equipment = new Equipment { EquipmentNumber = "PHOTO-E", Name = "照片设备", OwnershipType = EquipmentOwnershipType.SelfOwned, OwnerLegalEntity = company };
        db.AddRange(user, company, project, equipment); await db.SaveChangesAsync();
        var fileStore = new MemoryFileStore();
        var service = new EquipmentOfflineService(db, new EquipmentService(db), fileStore);
        var actor = EquipmentActor.Administrator(user.Id); var draftId = Guid.NewGuid();
        var usage = new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2026, 7, 1), null, RentMode.Daily, MonthlyProrationMode.ThirtyDay, 0m, false, null, [], null, "离线进场");
        await service.SyncAsync(actor, new EquipmentOfflineSyncRequest(draftId, Guid.NewGuid(), null, usage), default);
        var attachmentId = Guid.NewGuid();
        await using var firstStream = new MemoryStream([1, 2, 3]);
        var first = await service.SyncPhotoAsync(actor, new EquipmentOfflinePhotoRequest(draftId, attachmentId, "现场.jpg", "image/jpeg", 3, firstStream, AttachmentCategory.Photo, "现场照片"), default);
        await using var secondStream = new MemoryStream([1, 2, 3]);
        var second = await service.SyncPhotoAsync(actor, new EquipmentOfflinePhotoRequest(draftId, attachmentId, "现场.jpg", "image/jpeg", 3, secondStream, AttachmentCategory.Photo, "现场照片"), default);
        first.IsIdempotent.Should().BeFalse(); second.IsIdempotent.Should().BeTrue(); second.AttachmentId.Should().Be(first.AttachmentId);
        (await db.OfflineEquipmentAttachmentSyncs.CountAsync()).Should().Be(1);
    }

    private sealed class MemoryFileStore : IFileStore
    {
        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken) => Task.FromResult($"{Guid.NewGuid():N}.jpg");
        public Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string storedName, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
