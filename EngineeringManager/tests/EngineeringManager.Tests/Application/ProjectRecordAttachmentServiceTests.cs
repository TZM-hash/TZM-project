using System.Data;
using System.Data.Common;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectRecordAttachmentServiceTests
{
    [Fact]
    public async Task ReplaceQuantityAsyncKeepsOnlyNewestAttachment()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var directory = Path.Combine(Path.GetTempPath(), "engineering-attachment-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ProjectRecordAttachmentService(fixture.Db, new LocalFileStore(directory));
            var actor = new ProjectRecordAttachmentActor("attachment-user", true);

            var first = await service.UploadAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, "清单.pdf"), CancellationToken.None);
            var replacement = await service.ReplaceQuantityAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, "照片.jpg"), CancellationToken.None);

            var active = await service.ListAsync(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, CancellationToken.None);
            active.Should().ContainSingle().Which.Id.Should().Be(replacement.Id);
            (await fixture.Db.Attachments.SingleAsync(item => item.Id == first.Id)).IsDeleted.Should().BeTrue();
            (await fixture.Db.Attachments.SingleAsync(item => item.Id == replacement.Id)).IsDeleted.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ReplaceQuantityAsyncUsesSerializableTransaction()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var interceptor = new TransactionIsolationInterceptor();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(fixture.Connection)
                .AddInterceptors(interceptor)
                .Options);
        var directory = Path.Combine(Path.GetTempPath(), "engineering-attachment-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ProjectRecordAttachmentService(db, new LocalFileStore(directory));

            await service.ReplaceQuantityAsync(
                new ProjectRecordAttachmentActor("attachment-user", true),
                Upload(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, "并发替换.pdf"),
                CancellationToken.None);

            interceptor.IsolationLevel.Should().Be(IsolationLevel.Serializable);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ReplaceQuantityAsyncKeepsExistingAttachmentWhenStorageFails()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var directory = Path.Combine(Path.GetTempPath(), "engineering-attachment-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var actor = new ProjectRecordAttachmentActor("attachment-user", true);
            var workingService = new ProjectRecordAttachmentService(fixture.Db, new LocalFileStore(directory));
            var first = await workingService.UploadAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, "原附件.pdf"), CancellationToken.None);
            var failingService = new ProjectRecordAttachmentService(fixture.Db, new FailingFileStore());

            var action = () => failingService.ReplaceQuantityAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, "新附件.pdf"), CancellationToken.None);

            await action.Should().ThrowAsync<IOException>();
            var active = await workingService.ListAsync(fixture.Project.Id, ProjectRecordAttachmentType.Quantity, fixture.LineItem.Id, CancellationToken.None);
            active.Should().ContainSingle().Which.Id.Should().Be(first.Id);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task UploadAsyncStillAllowsMultipleAttachmentsForOtherRecordTypes()
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

            await service.UploadAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Construction, construction.Id, "进场记录.pdf"), CancellationToken.None);
            await service.UploadAsync(actor, Upload(fixture.Project.Id, ProjectRecordAttachmentType.Construction, construction.Id, "退场记录.pdf"), CancellationToken.None);

            (await service.ListAsync(fixture.Project.Id, ProjectRecordAttachmentType.Construction, construction.Id, CancellationToken.None)).Should().HaveCount(2);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ProjectCollectionAttachmentDoesNotRequireASettlementAllocation()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var collection = new FinanceCashEntry
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            CashType = LedgerCashType.Collection,
            LegalEntityId = fixture.LegalEntity.Id,
            BusinessPartnerId = fixture.Client.Id,
            BusinessDate = new DateOnly(2026, 7, 21),
            Amount = 10m,
            SourceType = LedgerSourceType.ProjectCollection,
            SourceId = fixture.Project.Id
        };
        fixture.Db.FinanceCashEntries.Add(collection);
        await fixture.Db.SaveChangesAsync();
        var directory = Path.Combine(Path.GetTempPath(), "engineering-attachment-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ProjectRecordAttachmentService(fixture.Db, new LocalFileStore(directory));

            var saved = await service.ReplaceAsync(
                new ProjectRecordAttachmentActor("attachment-user", true),
                Upload(fixture.Project.Id, ProjectRecordAttachmentType.Cash, collection.Id, "超额收款.pdf"),
                CancellationToken.None);

            saved.RecordId.Should().Be(collection.Id);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static ProjectRecordAttachmentUpload Upload(Guid projectId, ProjectRecordAttachmentType type, Guid recordId, string name) =>
        new(projectId, type, recordId, name, "application/octet-stream", [1, 2, 3]);

    private sealed class FailingFileStore : IFileStore
    {
        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken) =>
            throw new IOException("模拟文件存储失败。");

        public Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storedName, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TransactionIsolationInterceptor : DbTransactionInterceptor
    {
        public IsolationLevel? IsolationLevel { get; private set; }

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            IsolationLevel = eventData.IsolationLevel;
            return ValueTask.FromResult(result);
        }
    }
}
