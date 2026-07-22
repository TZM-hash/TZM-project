using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task LineItemWithConfiguredCompanyAndCustomerPostsCentralReceivable()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var legalEntity = await fixture.AddLegalEntityAsync();
        var partner = new BusinessPartner { PartnerNumber = "QTY-BP", Name = "工程量客户", ShortName = "客户" };
        var project = new Project { ProjectNumber = "QTY-P", Name = "工程量自动入账项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = legalEntity, IsPrimary = true });
        var contract = new Contract { Project = project, BusinessPartner = partner, ContractNumber = "QTY-C", Name = "工程量合同", TotalAmount = 1_000m };
        project.Contracts.Add(contract);
        fixture.Db.AddRange(partner, project);
        await fixture.Db.SaveChangesAsync();

        var line = await fixture.Service.AddLineItemAsync(
            new CreateContractLineItemRequest(contract.Id, "001", "暂估工程量", "项", 2m, 300m, null, null, false),
            CancellationToken.None);

        var settlement = await fixture.Db.FinanceSettlements.SingleAsync(item => item.SourceId == line.Id);
        settlement.SettlementState.Should().Be(LedgerSettlementState.Provisional);
        settlement.OriginalAmount.Should().Be(600m);
        settlement.BusinessPartnerId.Should().Be(partner.Id);
        settlement.LegalEntityId.Should().Be(legalEntity.Id);
    }

    [Fact]
    public async Task ProjectQueryCanIncludeInactiveWithoutBypassingUserScope()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var manager = new ApplicationUser { Id = "inactive-scope-manager", UserName = "inactive-scope-manager", DisplayName = "停用项目负责人" };
        fixture.Db.Users.Add(manager);
        fixture.Db.Projects.AddRange(
            new Project { ProjectNumber = "ACTIVE-SCOPE", Name = "活动项目", ResponsibleUser = manager, IsActive = true },
            new Project { ProjectNumber = "INACTIVE-SCOPE", Name = "停用项目", ResponsibleUser = manager, IsActive = false },
            new Project { ProjectNumber = "INACTIVE-HIDDEN", Name = "无权停用项目", IsActive = false });
        await fixture.Db.SaveChangesAsync();
        var actor = new ProjectListActor(manager.Id, false);
        var query = new ProjectListQuery(null, [], null, null, null, null, null, false);

        var activeOnly = await fixture.Service.SearchProjectsAsync(actor, query, CancellationToken.None);
        var includingInactive = await fixture.Service.SearchProjectsAsync(actor, query with { IncludeInactive = true }, CancellationToken.None);

        activeOnly.Items.Should().ContainSingle(item => item.Project.ProjectNumber == "ACTIVE-SCOPE");
        includingInactive.Items.Select(item => item.Project.ProjectNumber).Should().BeEquivalentTo(["ACTIVE-SCOPE", "INACTIVE-SCOPE"]);
        includingInactive.Items.Should().NotContain(item => item.Project.ProjectNumber == "INACTIVE-HIDDEN");
    }

    [Fact]
    public async Task SearchProjectsAppliesUserScopeAmountSortAndPaging()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var manager = new ApplicationUser { Id = "project-manager-a", UserName = "project-manager-a", DisplayName = "项目负责人甲" };
        fixture.Db.Users.Add(manager);
        for (var index = 1; index <= 24; index++)
        {
            var project = new Project
            {
                ProjectNumber = $"P-SEARCH-{index:00}",
                Name = $"市政道路 {index:00}",
                ResponsibleUserId = manager.Id,
                Stage = ProjectStage.UnderConstruction
            };
            var contract = new Contract { Project = project, ContractNumber = $"C-{index:00}", Name = "施工合同", TotalAmount = index * 2_000m };
            contract.LineItems.Add(new ContractLineItem { Contract = contract, Code = "001", Name = "工程量", Unit = "项", EstimatedQuantity = index, EstimatedUnitPrice = 1_000m });
            project.Contracts.Add(contract);
            fixture.Db.Projects.Add(project);
        }
        fixture.Db.Projects.Add(new Project { ProjectNumber = "P-HIDDEN", Name = "市政隐藏项目", Stage = ProjectStage.UnderConstruction });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SearchProjectsAsync(
            new ProjectListActor(manager.Id, false),
            new ProjectListQuery("市政", [ProjectStage.UnderConstruction], null, null, 1_000m, 24_000m, "CurrentAmount", true, 2, 20),
            CancellationToken.None);

        result.TotalCount.Should().Be(24);
        result.Page.Should().Be(2);
        result.Items.Should().HaveCount(4);
        result.Items.Select(item => item.Summary.CurrentAmount).Should().BeInDescendingOrder();
        result.Items.Should().OnlyContain(item => item.Project.ProjectNumber != "P-HIDDEN");
        var options = await fixture.Service.GetListOptionsAsync(new ProjectListActor(manager.Id, false), CancellationToken.None);
        options.ResponsibleUsers.Should().ContainSingle(item => item.Value == manager.Id);
    }

    [Fact]
    public async Task ProjectSearchUsesRelatedCompanyPartnerAndContractFields()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var company = await fixture.AddLegalEntityAsync();
        var partner = new BusinessPartner { PartnerNumber = "BP-PROJECT-SEARCH", Name = "项目合作单位", ShortName = "项目合作" };
        fixture.Db.BusinessPartners.Add(partner);
        var project = await fixture.Service.CreateProjectAsync(new CreateProjectRequest("P-FULL-SEARCH", "全字段项目", "总包搜索", null, null, null, ProjectStage.UnderConstruction, [company.Id], Notes: "项目备注"), CancellationToken.None);
        var contract = await fixture.Service.AddContractAsync(new CreateContractRequest(project.Id, "C-FULL-SEARCH", "搜索合同", ContractType.MainContract, ContractAllocationMode.SingleCompany, "合同对方", 100m, [new ContractAllocationRequest(company.Id, 100m, null)], "合同备注"), CancellationToken.None);
        await fixture.Service.AddLineItemAsync(new CreateContractLineItemRequest(contract.Id, "LINE-SEARCH", "搜索清单", "项", 1m, 2m, null, null, false, "清单备注"), CancellationToken.None);
        fixture.Db.ProjectPartners.Add(new ProjectPartner { ProjectId = project.Id, BusinessPartnerId = partner.Id, RoleType = BusinessPartnerRoleType.MaterialSupplier });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SearchProjectsAsync(new ProjectListActor("administrator", true), new ProjectListQuery("项目合作 搜索清单", [], null, null, null, null, null, false), CancellationToken.None);

        result.Items.Should().ContainSingle(item => item.Project.Id == project.Id);
    }

    [Fact]
    public async Task DuplicateProjectNumberIsRejected()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var request = new CreateProjectRequest("P-SVC-01", "服务测试项目", null, null, null, null, ProjectStage.AwaitingMobilization, []);
        await fixture.Service.CreateProjectAsync(request, CancellationToken.None);

        var action = () => fixture.Service.CreateProjectAsync(request with { Name = "重复项目" }, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*项目编号*");
    }

    [Fact]
    public async Task NewProjectStartsWithNamedMainContractAndEmptyAmount()
    {
        await using var fixture = await ProjectFixture.CreateAsync();

        var project = await fixture.Service.CreateProjectAsync(
            new CreateProjectRequest("P-DEFAULT-CONTRACT", "默认主合同项目", null, null, null, null, ProjectStage.AwaitingMobilization, []),
            CancellationToken.None);

        var contract = await fixture.Db.Contracts.SingleAsync(item => item.ProjectId == project.Id);
        contract.ContractNumber.Should().Be("P-DEFAULT-CONTRACT-C01");
        contract.Name.Should().Be("默认主合同项目");
        contract.ContractType.Should().Be(ContractType.MainContract);
        contract.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task AffiliationTypeFilterSeparatesAttachedProjects()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        await fixture.Service.CreateProjectAsync(new CreateProjectRequest("P-AFF-01", "自营项目", null, null, null, null, ProjectStage.UnderConstruction, [], AffiliationType: ProjectAffiliationType.SelfOperated), CancellationToken.None);
        await fixture.Service.CreateProjectAsync(new CreateProjectRequest("P-AFF-02", "他方挂靠", null, null, null, null, ProjectStage.UnderConstruction, [], AffiliationType: ProjectAffiliationType.ExternalPartyAttachedToUs), CancellationToken.None);

        var result = await fixture.Service.SearchProjectsAsync(new ProjectListActor("administrator", true),
            new ProjectListQuery(null, [], null, null, null, null, null, false, 1, 20, ProjectAffiliationType.ExternalPartyAttachedToUs), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Project.ProjectNumber.Should().Be("P-AFF-02");
        result.Items[0].Project.AffiliationType.Should().Be(ProjectAffiliationType.ExternalPartyAttachedToUs);
    }

    [Fact]
    public async Task InvalidContractAllocationDoesNotSaveContract()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var legalEntity = await fixture.AddLegalEntityAsync();
        var project = await fixture.Service.CreateProjectAsync(
            new CreateProjectRequest("P-SVC-02", "合同校验项目", null, null, null, null, ProjectStage.AwaitingMobilization, [legalEntity.Id]),
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
        (await fixture.Db.Contracts.CountAsync(item => item.ProjectId == project.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ProjectSummaryUsesProjectStageToInterpretUnifiedValues()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var legalEntity = await fixture.AddLegalEntityAsync();
        var project = await fixture.Service.CreateProjectAsync(
            new CreateProjectRequest("P-SVC-03", "汇总项目", null, null, null, null, ProjectStage.PartiallySettled, [legalEntity.Id]),
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
            new CreateContractLineItemRequest(contract.Id, "001", "工程量一", "m", null, null, null, null, false, Quantity: 10m, UnitPrice: 5m),
            CancellationToken.None);
        await fixture.Service.AddLineItemAsync(
            new CreateContractLineItemRequest(contract.Id, "002", "工程量二", "m", null, null, null, null, false, Quantity: 3m, UnitPrice: 10m),
            CancellationToken.None);

        var details = await fixture.Service.GetProjectAsync(project.Id, CancellationToken.None);

        details.Should().NotBeNull();
        details!.Summary.ContractAmount.Should().Be(100m);
        details.Summary.EstimatedAmount.Should().Be(0m);
        details.Summary.SettledAmount.Should().Be(80m);
        details.Summary.CurrentAmount.Should().Be(80m);
        details.Summary.SettlementStatus.Should().Be(ProjectSettlementStatus.PartiallySettled);
    }

    [Fact]
    public async Task ExistingLineItemCanBeUpdatedInPlaceWithConcurrencyControl()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var legalEntity = await fixture.AddLegalEntityAsync();
        var project = await fixture.Service.CreateProjectAsync(
            new CreateProjectRequest("P-SVC-04", "原位编辑项目", null, null, null, null, ProjectStage.UnderConstruction, [legalEntity.Id]),
            CancellationToken.None);
        var contract = await fixture.Service.AddContractAsync(
            new CreateContractRequest(project.Id, "C-SVC-04", "原位编辑合同", ContractType.MainContract,
                ContractAllocationMode.SingleCompany, "测试总包", 100m, [new ContractAllocationRequest(legalEntity.Id, 100m, null)]),
            CancellationToken.None);
        var line = await fixture.Service.AddLineItemAsync(
            new CreateContractLineItemRequest(contract.Id, "001", "原工程量", "m", 10m, 5m, null, null, false),
            CancellationToken.None);

        var updated = await fixture.Service.UpdateLineItemAsync(
            new UpdateContractLineItemRequest(line.Id, "001-A", "修改后工程量", "m³", null, null, null, null, false, line.ConcurrencyStamp, Quantity: 12m, UnitPrice: 6m, AccountingLabel: "现场复核", RequiresInvoice: false),
            CancellationToken.None);

        updated.Code.Should().Be("001-A");
        updated.Name.Should().Be("修改后工程量");
        updated.Amount.Should().Be(72m);
        updated.AccountingLabel.Should().Be("现场复核");
        updated.RequiresInvoice.Should().BeFalse();
        updated.ConcurrencyStamp.Should().NotBe(line.ConcurrencyStamp);
    }

    [Fact]
    public async Task ProjectContractAndQuantityNotesRoundTrip()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var legalEntity = await fixture.AddLegalEntityAsync();
        var project = await fixture.Service.CreateProjectAsync(
            new CreateProjectRequest("P-NOTES", "备注项目", null, null, null, null, ProjectStage.UnderConstruction, [legalEntity.Id], Notes: "项目备注"),
            CancellationToken.None);
        var contract = await fixture.Service.AddContractAsync(
            new CreateContractRequest(project.Id, "C-NOTES", "备注合同", ContractType.MainContract, ContractAllocationMode.SingleCompany, "总包", 100m, [new ContractAllocationRequest(legalEntity.Id, 100m, null)], "合同备注"),
            CancellationToken.None);
        var line = await fixture.Service.AddLineItemAsync(
            new CreateContractLineItemRequest(contract.Id, "001", "备注工程量", "m", 1m, 2m, null, null, false, "工程量备注"),
            CancellationToken.None);
        var updatedLine = await fixture.Service.UpdateLineItemAsync(
            new UpdateContractLineItemRequest(line.Id, line.Code, line.Name, line.Unit, 2m, 3m, null, null, false, line.ConcurrencyStamp, "工程量更新备注", "admin", "修改工程量备注"),
            CancellationToken.None);

        project.Notes.Should().Be("项目备注");
        contract.Notes.Should().Be("合同备注");
        line.Notes.Should().Be("工程量备注");
        updatedLine.Notes.Should().Be("工程量更新备注");
        var audit = await fixture.Db.AuditLogs.SingleAsync(item => item.EntityType == nameof(ContractLineItem));
        audit.UserId.Should().Be("admin");
        audit.Reason.Should().Be("修改工程量备注");
        using var after = JsonDocument.Parse(audit.AfterJson!);
        after.RootElement.GetProperty("Notes").GetString().Should().Be("工程量更新备注");
    }

    [Fact]
    public async Task ProjectCanStoreMultipleTaxConfigurationsAndContractSigningStatus()
    {
        await using var fixture = await ProjectFixture.CreateAsync();

        var project = await fixture.Service.CreateProjectAsync(
            new CreateProjectRequest(
                "P-TAX-SVC",
                "多税率项目",
                null,
                null,
                null,
                null,
                ProjectStage.AwaitingMobilization,
                [],
                ContractSigningStatus: ContractSigningStatus.SentForSignature,
                TaxConfigurations:
                [
                    new ProjectTaxConfigurationInput(0.03m, ProjectInvoiceType.Special),
                    new ProjectTaxConfigurationInput(0.09m, ProjectInvoiceType.Ordinary)
                ]),
            CancellationToken.None);

        project.ContractSigningStatus.Should().Be(ContractSigningStatus.SentForSignature);
        project.TaxConfigurations.Should().BeEquivalentTo(
        [
            new { TaxRate = 0.03m, InvoiceType = ProjectInvoiceType.Special, IsActive = true },
            new { TaxRate = 0.09m, InvoiceType = ProjectInvoiceType.Ordinary, IsActive = true }
        ]);
    }

    [Fact]
    public async Task DuplicateProjectTaxConfigurationsAreRejected()
    {
        await using var fixture = await ProjectFixture.CreateAsync();
        var request = new CreateProjectRequest(
            "P-TAX-DUP",
            "重复税率项目",
            null,
            null,
            null,
            null,
            ProjectStage.AwaitingMobilization,
            [],
            TaxConfigurations:
            [
                new ProjectTaxConfigurationInput(0.03m, ProjectInvoiceType.Special),
                new ProjectTaxConfigurationInput(0.03m, ProjectInvoiceType.Special)
            ]);

        var action = () => fixture.Service.CreateProjectAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*重复*");
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
