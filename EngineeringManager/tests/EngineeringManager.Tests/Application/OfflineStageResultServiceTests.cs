using EngineeringManager.Application.Offline;
using EngineeringManager.Domain.Offline;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Infrastructure.Offline;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class OfflineStageResultServiceTests
{
    [Fact]
    public async Task FirstSyncCreatesOneDraftAndSameOperationIsIdempotent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Request(Guid.NewGuid(), Guid.NewGuid());

        var first = await fixture.Service.SyncDraftAsync(fixture.Actor, request, default);
        var repeated = await fixture.Service.SyncDraftAsync(fixture.Actor, request, default);

        first.Status.Should().Be(OfflineSyncStatus.Synced);
        repeated.IsIdempotent.Should().BeTrue();
        repeated.ServerStageResultId.Should().Be(first.ServerStageResultId);
        (await fixture.Db.StageResults.CountAsync()).Should().Be(1);
        (await fixture.Db.OfflineDraftSyncs.SingleAsync()).LastOperationId.Should().Be(request.OperationId);
    }

    [Fact]
    public async Task MatchingVersionUpdatesDraftAndStaleVersionReturnsConflictSnapshot()
    {
        await using var fixture = await Fixture.CreateAsync();
        var clientDraftId = Guid.NewGuid();
        var created = await fixture.Service.SyncDraftAsync(fixture.Actor, fixture.Request(clientDraftId, Guid.NewGuid()), default);
        var update = fixture.Request(clientDraftId, Guid.NewGuid()) with
        {
            ServerStageResultId = created.ServerStageResultId,
            BaseServerVersion = created.ServerVersion,
            Title = "联网后更新"
        };
        var updated = await fixture.Service.SyncDraftAsync(fixture.Actor, update, default);
        var stale = await fixture.Service.SyncDraftAsync(fixture.Actor, update with { OperationId = Guid.NewGuid(), Title = "过期内容" }, default);

        updated.Status.Should().Be(OfflineSyncStatus.Synced);
        updated.ServerVersion.Should().NotBe(created.ServerVersion);
        stale.Status.Should().Be(OfflineSyncStatus.Conflict);
        stale.ServerSnapshot!.Title.Should().Be("联网后更新");
        (await fixture.Db.StageResults.SingleAsync()).Title.Should().Be("联网后更新");
    }

    [Fact]
    public async Task UserCannotSyncUnassignedProjectButAdministratorCan()
    {
        await using var fixture = await Fixture.CreateAsync(assignUser: false);
        var request = fixture.Request(Guid.NewGuid(), Guid.NewGuid());

        var denied = () => fixture.Service.SyncDraftAsync(fixture.Actor, request, default);
        await denied.Should().ThrowAsync<UnauthorizedAccessException>();

        var adminResult = await fixture.Service.SyncDraftAsync(fixture.Actor with { CanAccessAllProjects = true }, request, default);
        adminResult.Status.Should().Be(OfflineSyncStatus.Synced);
    }

    [Fact]
    public async Task PhotoRetryReturnsExistingAttachmentAndDoesNotDuplicateFile()
    {
        await using var fixture = await Fixture.CreateAsync();
        var clientDraftId = Guid.NewGuid();
        await fixture.Service.SyncDraftAsync(fixture.Actor, fixture.Request(clientDraftId, Guid.NewGuid()), default);
        var photoId = Guid.NewGuid();

        var first = await fixture.Service.SyncPhotoAsync(
            fixture.Actor,
            new OfflinePhotoSyncRequest(clientDraftId, photoId, "现场.jpg", "image/jpeg", 100, new MemoryStream(new byte[100]), AttachmentCategory.Photo, null),
            default);
        var repeated = await fixture.Service.SyncPhotoAsync(
            fixture.Actor,
            new OfflinePhotoSyncRequest(clientDraftId, photoId, "现场.jpg", "image/jpeg", 100, new MemoryStream(new byte[100]), AttachmentCategory.Photo, null),
            default);

        repeated.IsIdempotent.Should().BeTrue();
        repeated.AttachmentId.Should().Be(first.AttachmentId);
        (await fixture.Db.Attachments.CountAsync()).Should().Be(1);
        Directory.GetFiles(fixture.AttachmentRoot).Should().ContainSingle();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public IOfflineStageResultService Service { get; }
        public OfflineSyncActor Actor { get; }
        public Project Project { get; }
        public Contract Contract { get; }
        public ContractLineItem LineItem { get; }
        public string AttachmentRoot { get; }

        private Fixture(
            SqliteConnection connection,
            ApplicationDbContext db,
            IOfflineStageResultService service,
            OfflineSyncActor actor,
            Project project,
            Contract contract,
            ContractLineItem lineItem,
            string attachmentRoot)
        {
            this.connection = connection;
            Db = db;
            Service = service;
            Actor = actor;
            Project = project;
            Contract = contract;
            LineItem = lineItem;
            AttachmentRoot = attachmentRoot;
        }

        public static async Task<Fixture> CreateAsync(bool assignUser = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var user = new ApplicationUser { UserName = "offline-user", NormalizedUserName = "OFFLINE-USER", DisplayName = "现场用户", IsEnabled = true };
            var project = new Project { ProjectNumber = "P-OFF-SVC", Name = "离线同步项目", Stage = ProjectStage.UnderConstruction, ResponsibleUser = assignUser ? user : null };
            var contract = new Contract { Project = project, ContractNumber = "C-OFF-SVC", Name = "施工合同", TotalAmount = 1000m };
            var lineItem = new ContractLineItem { Contract = contract, Code = "001", Name = "工程量", Unit = "m", EstimatedQuantity = 100m, EstimatedUnitPrice = 10m };
            contract.LineItems.Add(lineItem);
            project.Contracts.Add(contract);
            db.AddRange(user, project);
            await db.SaveChangesAsync();
            var attachmentRoot = Path.Combine(Path.GetTempPath(), $"engineering-manager-offline-{Guid.NewGuid():N}");
            var service = new OfflineStageResultService(db, new LocalFileStore(attachmentRoot));
            return new Fixture(connection, db, service, new OfflineSyncActor(user.Id, false), project, contract, lineItem, attachmentRoot);
        }

        public OfflineDraftSyncRequest Request(Guid clientDraftId, Guid operationId) => new(
            clientDraftId,
            operationId,
            null,
            null,
            Project.Id,
            Contract.Id,
            "离线阶段成果",
            StageResultType.Progress,
            new DateOnly(2026, 7, 16),
            "现场备注",
            QualityResult.NotChecked,
            [new OfflineLineItemRequest(LineItem.Id, 10m, "本期完成")]);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(AttachmentRoot)) Directory.Delete(AttachmentRoot, recursive: true);
        }
    }
}
