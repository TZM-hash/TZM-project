using EngineeringManager.Application.StageResults;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.StageResults;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class StageResultServiceTests
{
    [Fact]
    public async Task DraftQuantitiesDoNotEnterLaterRecordedCumulativeQuantity()
    {
        await using var fixture = await StageFixture.CreateAsync(100m);
        await fixture.CreateAsync(StageResultStatus.Recorded, 20m, "第一次记录");
        await fixture.CreateAsync(StageResultStatus.Draft, 10m, "离线草稿");

        var result = await fixture.CreateAsync(StageResultStatus.Recorded, 5m, "第二次记录");

        result.Lines.Single().CumulativeQuantity.Should().Be(25m);
        result.Lines.Single().RemainingQuantity.Should().Be(75m);
    }

    [Fact]
    public async Task OverTargetQuantityIsKeptAndMarkedAsRisk()
    {
        await using var fixture = await StageFixture.CreateAsync(100m);
        await fixture.CreateAsync(StageResultStatus.Recorded, 90m, "累计九十");

        var result = await fixture.CreateAsync(StageResultStatus.Recorded, 20m, "超量记录");

        result.Lines.Single().CumulativeQuantity.Should().Be(110m);
        result.Lines.Single().ExceedsTarget.Should().BeTrue();
    }

    [Fact]
    public async Task AttachmentDtoDoesNotExposeStoredNameOrPhysicalPath()
    {
        await using var fixture = await StageFixture.CreateAsync(100m);

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(
                StageResultStatus.Recorded,
                10m,
                "带附件记录",
                [new StageAttachmentRequest("guid-photo.jpg", "现场照片.jpg", "image/jpeg", 1024, AttachmentCategory.Photo, "进度照片")]),
            CancellationToken.None);

        result.Attachments.Should().ContainSingle().Which.OriginalFileName.Should().Be("现场照片.jpg");
        typeof(StageAttachmentDto).GetProperty("StoredName").Should().BeNull();
        typeof(StageAttachmentDto).GetProperty("PhysicalPath").Should().BeNull();
    }

    private sealed class StageFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private StageFixture(SqliteConnection connection, ApplicationDbContext db, IStageResultService service, Guid projectId, Guid contractId, Guid lineItemId)
        {
            this.connection = connection;
            Db = db;
            Service = service;
            ProjectId = projectId;
            ContractId = contractId;
            LineItemId = lineItemId;
        }

        public ApplicationDbContext Db { get; }
        public IStageResultService Service { get; }
        public Guid ProjectId { get; }
        public Guid ContractId { get; }
        public Guid LineItemId { get; }

        public static async Task<StageFixture> CreateAsync(decimal targetQuantity)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var project = new Project { ProjectNumber = $"P-{Guid.NewGuid():N}"[..12], Name = "阶段服务项目", Stage = ProjectStage.UnderConstruction };
            var contract = new Contract { Project = project, ContractNumber = "C-01", Name = "阶段合同", TotalAmount = 100m };
            var lineItem = new ContractLineItem { Contract = contract, Code = "001", Name = "测试清单", Unit = "m", EstimatedQuantity = targetQuantity, EstimatedUnitPrice = 1m };
            contract.LineItems.Add(lineItem);
            project.Contracts.Add(contract);
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return new StageFixture(connection, db, new StageResultService(db), project.Id, contract.Id, lineItem.Id);
        }

        public CreateStageResultRequest CreateRequest(
            StageResultStatus status,
            decimal periodQuantity,
            string title,
            IReadOnlyCollection<StageAttachmentRequest>? attachments = null) =>
            new(
                ProjectId,
                ContractId,
                title,
                StageResultType.Progress,
                status,
                new DateOnly(2026, 7, 16),
                null,
                QualityResult.NotChecked,
                null,
                status == StageResultStatus.Draft,
                [new StageResultLineRequest(LineItemId, periodQuantity, null)],
                attachments ?? []);

        public Task<StageResultDto> CreateAsync(StageResultStatus status, decimal periodQuantity, string title) =>
            Service.CreateAsync(CreateRequest(status, periodQuantity, title), CancellationToken.None);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
