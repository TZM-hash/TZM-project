using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectWorkspaceServiceTests
{
    [Fact]
    public async Task WorkspaceCombinesQuantityFinanceDetailsAndActivity()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();

        var workspace = await fixture.Service.GetAsync(fixture.Project.Id, CancellationToken.None);

        workspace.Should().NotBeNull();
        workspace!.Overview.AffiliationType.Should().Be(ProjectAffiliationType.ExternalPartyAttachedToUs);
        workspace.ProjectSummary.EstimatedAmount.Should().Be(200m);
        workspace.Contracts.Should().ContainSingle().Which.LineItems.Should().ContainSingle(item => item.EstimatedAmount == 200m);
        workspace.FinanceSummary.ReceivableAmount.Should().Be(100m);
        workspace.FinanceSummary.CollectedAmount.Should().Be(40m);
        workspace.FinanceSummary.UncollectedAmount.Should().Be(60m);
        workspace.FinanceSummary.OutputInvoiceAmount.Should().Be(30m);
        workspace.FinanceSummary.PayableAmount.Should().Be(80m);
        workspace.FinanceSummary.PaidAmount.Should().Be(25m);
        workspace.Receivables.Should().ContainSingle(item => item.Amount == 100m);
        workspace.Collections.Should().ContainSingle(item => item.Amount == 40m);
        workspace.Invoices.Should().ContainSingle(item => item.GrossAmount == 30m);
        workspace.Payables.Should().ContainSingle(item => item.Amount == 80m);
        workspace.Payments.Should().ContainSingle(item => item.Amount == 25m);
        workspace.Activities.Should().Contain(item => item.Title == "存在未收款");
        workspace.Activities.Should().Contain(item => item.Title == "收款 40.00");
    }

    [Fact]
    public async Task UpdateChangesAffiliationAndLegalEntitiesAndWritesAuditLog()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var originalStamp = fixture.Project.ConcurrencyStamp;

        var updated = await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id,
                fixture.Project.ProjectNumber,
                "更新后的项目名称",
                "上级项目",
                "新总包单位",
                "联系人",
                "13800000000",
                null,
                null,
                null,
                ProjectStage.Settlement,
                ProjectAffiliationType.WeAttachedToExternalParty,
                ArchiveStatus.PendingArchive,
                [fixture.SecondLegalEntity.Id, fixture.LegalEntity.Id],
                originalStamp,
                "调整合作方式和签约公司"),
            CancellationToken.None);

        updated.Overview.Name.Should().Be("更新后的项目名称");
        updated.Overview.AffiliationType.Should().Be(ProjectAffiliationType.WeAttachedToExternalParty);
        updated.Overview.ConcurrencyStamp.Should().NotBe(originalStamp);
        var links = await fixture.Db.ProjectLegalEntities.AsNoTracking().Where(item => item.ProjectId == fixture.Project.Id).ToListAsync();
        links.Should().HaveCount(2);
        links.Should().ContainSingle(item => item.LegalEntityId == fixture.SecondLegalEntity.Id && item.IsPrimary);
        links.Should().ContainSingle(item => item.LegalEntityId == fixture.LegalEntity.Id && !item.IsPrimary);
        var audit = await fixture.Db.AuditLogs.AsNoTracking().SingleAsync(item => item.Action == "UpdateProject");
        audit.UserId.Should().Be("workspace-user");
        audit.Reason.Should().Be("调整合作方式和签约公司");
        audit.BeforeJson.Should().Contain("\"AffiliationType\":2");
        audit.AfterJson.Should().Contain("\"AffiliationType\":3");
        updated.Activities.Should().Contain(item => item.Title == "编辑项目资料" && item.UserName == "项目管理员");
    }

    [Fact]
    public async Task UpdateRejectsStaleConcurrencyStamp()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var request = new UpdateProjectRequest(
            fixture.Project.Id,
            fixture.Project.ProjectNumber,
            fixture.Project.Name,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            fixture.Project.Stage,
            fixture.Project.AffiliationType,
            fixture.Project.ArchiveStatus,
            [fixture.LegalEntity.Id],
            Guid.NewGuid(),
            "并发测试");

        var action = () => fixture.Service.UpdateAsync(new ProjectWorkspaceActor("workspace-user", "项目管理员"), request, CancellationToken.None);

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>().WithMessage("*刷新后重试*");
    }

    private sealed class ProjectWorkspaceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ProjectWorkspaceFixture(SqliteConnection connection, ApplicationDbContext db)
        {
            this.connection = connection;
            Db = db;
            Service = new ProjectWorkspaceService(db);
        }

        public ApplicationDbContext Db { get; }
        public ProjectWorkspaceService Service { get; }
        public LegalEntity LegalEntity { get; private set; } = null!;
        public LegalEntity SecondLegalEntity { get; private set; } = null!;
        public Project Project { get; private set; } = null!;

        public static async Task<ProjectWorkspaceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new ProjectWorkspaceFixture(connection, db);
            await fixture.SeedAsync();
            return fixture;
        }

        private async Task SeedAsync()
        {
            LegalEntity = new LegalEntity { Code = "WORK-LE-01", Name = "工作台测试一公司", ShortName = "测试一公司" };
            SecondLegalEntity = new LegalEntity { Code = "WORK-LE-02", Name = "工作台测试二公司", ShortName = "测试二公司" };
            var partner = new BusinessPartner { PartnerNumber = "WORK-BP-01", Name = "工作台测试合作单位", ShortName = "测试合作单位" };
            Project = new Project
            {
                ProjectNumber = "WORK-P-01",
                Name = "工作台测试项目",
                Stage = ProjectStage.UnderConstruction,
                AffiliationType = ProjectAffiliationType.ExternalPartyAttachedToUs
            };
            var contract = new Contract
            {
                Project = Project,
                BusinessPartner = partner,
                ContractNumber = "WORK-C-01",
                Name = "工作台测试合同",
                ContractType = ContractType.MainContract,
                TotalAmount = 260m
            };
            contract.LineItems.Add(new ContractLineItem
            {
                Contract = contract,
                Code = "001",
                Name = "土方工程",
                Unit = "m³",
                EstimatedQuantity = 10m,
                EstimatedUnitPrice = 20m
            });
            Project.Contracts.Add(contract);
            Project.LegalEntities.Add(new ProjectLegalEntity { Project = Project, LegalEntity = LegalEntity, IsPrimary = true });
            var account = new FinancialAccount { LegalEntity = LegalEntity, AccountName = "工作台测试账户", AccountType = FinancialAccountType.Bank };
            var receivable = new ReceivableEntry { Project = Project, Contract = contract, LegalEntity = LegalEntity, BusinessPartner = partner, EntryDate = new DateOnly(2026, 7, 1), Amount = 100m, Description = "进度应收" };
            var payable = new PayableEntry { Project = Project, Contract = contract, LegalEntity = LegalEntity, BusinessPartner = partner, EntryDate = new DateOnly(2026, 7, 2), Amount = 80m, Description = "班组应付" };
            Db.AddRange(
                LegalEntity,
                SecondLegalEntity,
                partner,
                Project,
                account,
                receivable,
                payable,
                new CollectionEntry { Receivable = receivable, Project = Project, Contract = contract, LegalEntity = LegalEntity, BusinessPartner = partner, Account = account, CollectionDate = new DateOnly(2026, 7, 5), Amount = 40m, Notes = "首笔到账" },
                new InvoiceEntry { Project = Project, Contract = contract, LegalEntity = LegalEntity, BusinessPartner = partner, Direction = InvoiceDirection.Output, InvoiceNumber = "INV-WORK-01", InvoiceDate = new DateOnly(2026, 7, 6), NetAmount = 28.30m, TaxAmount = 1.70m, GrossAmount = 30m, Status = InvoiceStatus.IssuedOrReceived },
                new PaymentEntry { Payable = payable, Project = Project, Contract = contract, LegalEntity = LegalEntity, BusinessPartner = partner, Account = account, PaymentDate = new DateOnly(2026, 7, 7), Amount = 25m, Notes = "首笔付款" });
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
