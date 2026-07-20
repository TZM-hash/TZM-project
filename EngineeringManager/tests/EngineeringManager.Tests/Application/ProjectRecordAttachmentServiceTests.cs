using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectRecordAttachmentServiceTests
{
    [Fact]
    public async Task QuantityAndConstructionRecordsEachSupportMultipleScopedAttachments()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var equipment = new Equipment { EquipmentNumber = "ATT-EQ", Name = "附件设备" };
        var construction = new ProjectConstructionRecord { Project = fixture.Project, RecordType = ProjectConstructionRecordType.Equipment, Equipment = equipment };
        fixture.Db.AddRange(equipment, construction);
        await fixture.Db.SaveChangesAsync();
        var directory = Path.Combine(Path.GetTempPath(), "engineering-attachment-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ProjectRecordAttachmentService(fixture.Db, new LocalFileStore(directory));
            var actor = new ProjectRecordAttachmentActor("attachment-user", true);

            var first = await service.UploadAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, "清单.pdf"), CancellationToken.None);
            await service.UploadAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, "照片.jpg"), CancellationToken.None);
            await service.UploadAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Construction, construction.Id, "进场记录.pdf"), CancellationToken.None);

            (await service.ListAsync(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, CancellationToken.None)).Should().HaveCount(2);
            (await service.ListAsync(fixture.Project.Id, ProjectRecordAttachmentType.Construction, construction.Id, CancellationToken.None)).Should().ContainSingle();
            var download = await service.DownloadAsync(fixture.Project.Id, first.Id, CancellationToken.None);
            await using var downloadedContent = download.Content;
            download.OriginalFileName.Should().Be("清单.pdf");
            await service.DeleteAsync(actor, fixture.Project.Id, first.Id, CancellationToken.None);
            (await service.ListAsync(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, CancellationToken.None)).Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static ProjectRecordAttachmentUpload Upload(Guid projectId, ProjectRecordAttachmentType type, Guid recordId, string name) =>
        new(projectId, type, recordId, name, "application/octet-stream", [1, 2, 3]);
}
