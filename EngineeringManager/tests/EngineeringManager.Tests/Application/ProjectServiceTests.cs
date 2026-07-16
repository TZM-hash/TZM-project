using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task DuplicateProjectNumberIsRejected()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var request = new CreateProjectRequest("P-SVC-01", "服务测试项目", null, null, null, null, ProjectStage.Preliminary, ArchiveStatus.NotArchived, []);
        await fixture.Service.CreateProjectAsync(request, CancellationToken.None);

        var action = () => fixture.Service.CreateProjectAsync(request with { Name = "重复项目" }, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*项目编号*");
    }

    [Fact]
    public async Task InvalidContractAllocationDoesNotSaveContract()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var legalEntity = await fixture.AddLegalEntityAsync();
        var project = await fixture.Service.CreateProjectAsync(
            new CreateProjectRequest("P-SVC-02", "合同校验项目", null, null, null, null, ProjectStage.AwaitingContract, ArchiveStatus.NotArchived, [legalEntity.Id]),
            CancellationToken.None);
        var request = new CreateContractRequest(
            project.Id,
            "C-SVC-01",
            "校验合同",
            ContractType.MainContract,
            ContractAllocationMode.FixedAmount,
            "测试总包",
            100m,
            [new ContractAllocationRequest(legalEntity.Id, 80m, null)]);

        var action = () => fixture.Service.AddContractAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
        (await fixture.Db.Contracts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProjectSummaryUsesSettledValuesOnlyForConfirmedLines()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var legalEntity = await fixture.AddLegalEntityAsync();
        var project = await fixture.Service.CreateProjectAsync(
            new CreateProjectRequest("P-SVC-03", "汇总项目", null, null, null, null, ProjectStage.Settlement, ArchiveStatus.NotArchived, [legalEntity.Id]),
            CancellationToken.None);
        var contract = await fixture.Service.AddContractAsync(
            new CreateContractRequest(
                project.Id,
                "C-SVC-03",
                "汇总合同",
                ContractType.MainContract,
                ContractAllocationMode.SingleCompany,
                "测试总包",
                100m,
                [new ContractAllocationRequest(legalEntity.Id, 100m, null)]),
            CancellationToken.None);
        await fixture.Service.AddLineItemAsync(
            new CreateContractLineItemRequest(contract.Id, "001", "未结算项", "m", 10m, 5m, 9m, 5m, false),
            CancellationToken.None);
        await fixture.Service.AddLineItemAsync(
            new CreateContractLineItemRequest(contract.Id, "002", "已结算项", "m", 4m, 8m, 3m, 10m, true),
            CancellationToken.None);

        var details = await fixture.Service.GetProjectAsync(project.Id, CancellationToken.None);

        details.Should().NotBeNull();
        details!.Summary.ContractAmount.Should().Be(100m);
        details.Summary.EstimatedAmount.Should().Be(82m);
        details.Summary.SettledAmount.Should().Be(30m);
        details.Summary.CurrentAmount.Should().Be(80m);
        details.Summary.SettlementStatus.Should().Be(ProjectSettlementStatus.PartiallySettled);
    }

    private sealed class ProjectFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ProjectFixture(SqliteConnection connection, ApplicationDbContext db, IProjectService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IProjectService Service { get; }

        public static async Task<ProjectFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new ProjectFixture(connection, db, new ProjectService(db));
        }

        public async Task<LegalEntity> AddLegalEntityAsync()
        {
            var legalEntity = new LegalEntity
            {
                Code = $"LE-{Guid.NewGuid():N}"[..12],
                Name = "服务测试签约公司",
                ShortName = "服务公司"
            };
            Db.LegalEntities.Add(legalEntity);
            await Db.SaveChangesAsync();
            return legalEntity;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
