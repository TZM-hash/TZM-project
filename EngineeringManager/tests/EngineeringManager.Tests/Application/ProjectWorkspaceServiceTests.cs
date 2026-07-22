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
    public async Task WorkspaceIncludesMilestoneAssignmentAndPartnerNotes()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var user = new ApplicationUser { Id = "project-member", UserName = "project-member", DisplayName = "项目成员", IsEnabled = true };
        var partner = await fixture.Db.BusinessPartners.SingleAsync();
        var milestone = new ProjectMilestone { ProjectId = fixture.Project.Id, Name = "节点一", PlannedDate = new DateOnly(2026, 8, 1), Notes = "节点备注" };
        var assignment = new ProjectAssignment { ProjectId = fixture.Project.Id, User = user, UserId = user.Id, AssignmentType = ProjectAssignmentType.SiteStaff, Notes = "人员备注" };
        var projectPartner = new ProjectPartner { ProjectId = fixture.Project.Id, BusinessPartnerId = partner.Id, RoleType = EngineeringManager.Domain.Partners.BusinessPartnerRoleType.ConstructionCrew, Notes = "合作备注" };
        fixture.Db.AddRange(user, milestone, assignment, projectPartner);
        await fixture.Db.SaveChangesAsync();

        var workspace = await fixture.Service.GetAsync(fixture.Project.Id, CancellationToken.None);

        workspace!.Milestones.Should().ContainSingle(item => item.Notes == "节点备注");
        workspace.Assignments.Should().ContainSingle(item => item.UserName == "项目成员" && item.Notes == "人员备注");
        workspace.Partners.Should().ContainSingle(item => item.PartnerName == partner.Name && item.Notes == "合作备注");
    }

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
    public async Task UpdatePersistsOneToThreeProjectContractQuickEditsAndKeepsMainContractFirst()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var existing = await fixture.Db.Contracts.SingleAsync(item => item.ProjectId == fixture.Project.Id);

        var updated = await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id,
                fixture.Project.ProjectNumber,
                fixture.Project.Name,
                fixture.Project.ParentProjectName,
                fixture.Project.GeneralContractorName,
                fixture.Project.GeneralContractorContact,
                fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId,
                fixture.Project.DepartmentId,
                fixture.Project.BranchId,
                fixture.Project.Stage,
                fixture.Project.AffiliationType,
                [fixture.LegalEntity.Id],
                fixture.Project.ConcurrencyStamp,
                "维护项目合同",
                Contracts:
                [
                    new ProjectContractQuickEditInput(existing.Id, "主合同更新", 350m, existing.ConcurrencyStamp),
                    new ProjectContractQuickEditInput(null, "第二份合同", 125m, Guid.Empty)
                ]),
            CancellationToken.None);

        updated.Contracts.Should().HaveCount(2);
        updated.Contracts[0].Name.Should().Be("主合同更新");
        updated.Contracts[0].ContractType.Should().Be(ContractType.MainContract);
        updated.Contracts[0].TotalAmount.Should().Be(350m);
        updated.Contracts[1].Name.Should().Be("第二份合同");
        updated.Contracts[1].TotalAmount.Should().Be(125m);
        updated.ProjectSummary.ContractAmount.Should().Be(475m);
        updated.Contracts[1].ContractNumber.Should().Be("WORK-P-01-C02");
    }

    [Fact]
    public async Task UpdateCanRemoveUnlinkedProjectContractWhileKeepingAtLeastOne()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var existing = await fixture.Db.Contracts.SingleAsync(item => item.ProjectId == fixture.Project.Id);
        // remove linked line items so existing main contract can stay and second can be deleted
        var linked = fixture.Db.ContractLineItems.Where(item => item.ContractId == existing.Id);
        fixture.Db.ContractLineItems.RemoveRange(linked);
        var second = new Contract
        {
            ProjectId = fixture.Project.Id,
            ContractNumber = "WORK-P-01-C02",
            Name = "可删除合同",
            ContractType = ContractType.Supplement,
            AllocationMode = ContractAllocationMode.SingleCompany,
            TotalAmount = 50m
        };
        fixture.Db.Contracts.Add(second);
        await fixture.Db.SaveChangesAsync();
        existing = await fixture.Db.Contracts.SingleAsync(item => item.Id == existing.Id);

        var updated = await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id,
                fixture.Project.ProjectNumber,
                fixture.Project.Name,
                fixture.Project.ParentProjectName,
                fixture.Project.GeneralContractorName,
                fixture.Project.GeneralContractorContact,
                fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId,
                fixture.Project.DepartmentId,
                fixture.Project.BranchId,
                fixture.Project.Stage,
                fixture.Project.AffiliationType,
                [fixture.LegalEntity.Id],
                fixture.Project.ConcurrencyStamp,
                "删除未关联合同",
                Contracts:
                [
                    new ProjectContractQuickEditInput(existing.Id, existing.Name, existing.TotalAmount, existing.ConcurrencyStamp)
                ]),
            CancellationToken.None);

        updated.Contracts.Should().ContainSingle();
        updated.Contracts[0].Id.Should().Be(existing.Id);
        (await fixture.Db.Contracts.CountAsync(item => item.ProjectId == fixture.Project.Id && item.IsActive)).Should().Be(1);
        (await fixture.Db.Contracts.SingleAsync(item => item.Id == second.Id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ProjectInvoiceDetailsOnlyIncludeOutputInvoices()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var payable = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Direction == LedgerDirection.Payable);
        var inputInvoice = new FinanceInvoice
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Payable,
            LegalEntityId = payable.LegalEntityId,
            BusinessPartnerId = payable.BusinessPartnerId,
            InvoiceNumber = "INV-WORK-INPUT-01",
            InvoiceDate = new DateOnly(2026, 7, 8),
            Amount = 20m
        };
        inputInvoice.Allocations.Add(new FinanceInvoiceAllocation
        {
            Invoice = inputInvoice,
            Settlement = payable,
            ProjectId = fixture.Project.Id,
            ContractId = payable.ContractId,
            Amount = 20m,
            AllocationOrder = 1
        });
        fixture.Db.FinanceInvoices.Add(inputInvoice);
        await fixture.Db.SaveChangesAsync();

        var workspace = await fixture.Service.GetAsync(fixture.Project.Id, CancellationToken.None);

        workspace!.Invoices.Should().ContainSingle();
        workspace.Invoices.Single().Direction.Should().Be(InvoiceDirection.Output);
    }

    [Fact]
    public async Task WorkspaceOnlyIncludesEquipmentMarkedForProjectOverview()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var important = new Equipment { EquipmentNumber = "EQ-IMPORTANT", Name = "重要履带吊" };
        var ordinary = new Equipment { EquipmentNumber = "EQ-ORDINARY", Name = "普通挖机" };
        fixture.Db.AddRange(important, ordinary);
        fixture.Db.ProjectConstructionRecords.AddRange(
            new ProjectConstructionRecord
            {
                ProjectId = fixture.Project.Id,
                RecordType = ProjectConstructionRecordType.Equipment,
                Equipment = important,
                EntryDate = new DateOnly(2026, 7, 1),
                ShowInProjectOverview = true
            },
            new ProjectConstructionRecord
            {
                ProjectId = fixture.Project.Id,
                RecordType = ProjectConstructionRecordType.Equipment,
                Equipment = ordinary,
                EntryDate = new DateOnly(2026, 7, 2),
                ShowInProjectOverview = false
            });
        await fixture.Db.SaveChangesAsync();

        var workspace = await fixture.Service.GetAsync(fixture.Project.Id, CancellationToken.None);

        workspace!.OverviewEquipment.Should().NotBeNull();
        workspace.OverviewEquipment!.Should().ContainSingle();
        workspace.OverviewEquipment[0].EquipmentNumber.Should().Be("EQ-IMPORTANT");
        workspace.OverviewEquipment[0].EquipmentName.Should().Be("重要履带吊");
        workspace.OverviewEquipment[0].EntryDate.Should().Be(new DateOnly(2026, 7, 1));
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
                ProjectStage.CompletedUnsettled,
                ProjectAffiliationType.WeAttachedToExternalParty,
                [fixture.SecondLegalEntity.Id, fixture.LegalEntity.Id],
                originalStamp,
                "调整合作方式和签约公司",
                Notes: "项目总览备注"),
            CancellationToken.None);

        updated.Overview.Name.Should().Be("更新后的项目名称");
        updated.Overview.AffiliationType.Should().Be(ProjectAffiliationType.WeAttachedToExternalParty);
        updated.Overview.ConcurrencyStamp.Should().NotBe(originalStamp);
        updated.Overview.Notes.Should().Be("项目总览备注");
        var links = await fixture.Db.ProjectLegalEntities.AsNoTracking().Where(item => item.ProjectId == fixture.Project.Id).ToListAsync();
        links.Should().HaveCount(2);
        links.Should().ContainSingle(item => item.LegalEntityId == fixture.SecondLegalEntity.Id && item.IsPrimary);
        links.Should().ContainSingle(item => item.LegalEntityId == fixture.LegalEntity.Id && !item.IsPrimary);
        var audit = await fixture.Db.AuditLogs.AsNoTracking().SingleAsync(item => item.Action == "UpdateProject");
        audit.UserId.Should().Be("workspace-user");
        audit.Reason.Should().Be("调整合作方式和签约公司");
        audit.BeforeJson.Should().Contain("\"AffiliationType\":2");
        audit.AfterJson.Should().Contain("\"AffiliationType\":3");
        audit.AfterJson.Should().Contain("Notes");
        updated.Activities.Should().Contain(item => item.Title == "编辑项目资料" && item.UserName == "项目管理员");
    }

    [Fact]
    public async Task ChangingProjectToPartialSettlementFinalizesUnifiedQuantityPosting()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();

        await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id, fixture.Project.ProjectNumber, fixture.Project.Name, fixture.Project.ParentProjectName,
                fixture.Project.GeneralContractorName, fixture.Project.GeneralContractorContact, fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId, fixture.Project.DepartmentId, fixture.Project.BranchId,
                ProjectStage.PartiallySettled, fixture.Project.AffiliationType, [fixture.LegalEntity.Id],
                fixture.Project.ConcurrencyStamp, "项目进入部分结算"),
            CancellationToken.None);

        var posting = await fixture.Db.FinanceSettlements.SingleAsync(item => item.SourceType == LedgerSourceType.ProjectQuantity);
        posting.SettlementState.Should().Be(LedgerSettlementState.Final);
        posting.OriginalAmount.Should().Be(200m);
        posting.OriginalInvoiceAmount.Should().Be(200m);
    }

    [Fact]
    public async Task ChangingProjectBackFromSettlementRestoresUnifiedQuantityPostingToProvisional()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var settled = await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id, fixture.Project.ProjectNumber, fixture.Project.Name, fixture.Project.ParentProjectName,
                fixture.Project.GeneralContractorName, fixture.Project.GeneralContractorContact, fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId, fixture.Project.DepartmentId, fixture.Project.BranchId,
                ProjectStage.PartiallySettled, fixture.Project.AffiliationType, [fixture.LegalEntity.Id],
                fixture.Project.ConcurrencyStamp, "项目进入部分结算"),
            CancellationToken.None);

        await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id, fixture.Project.ProjectNumber, fixture.Project.Name, fixture.Project.ParentProjectName,
                fixture.Project.GeneralContractorName, fixture.Project.GeneralContractorContact, fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId, fixture.Project.DepartmentId, fixture.Project.BranchId,
                ProjectStage.UnderConstruction, fixture.Project.AffiliationType, [fixture.LegalEntity.Id],
                settled.Overview.ConcurrencyStamp, "项目退回施工阶段"),
            CancellationToken.None);

        var posting = await fixture.Db.FinanceSettlements.SingleAsync(item => item.SourceType == LedgerSourceType.ProjectQuantity);
        posting.SettlementState.Should().Be(LedgerSettlementState.Provisional);
        posting.OriginalAmount.Should().Be(200m);
    }

    [Fact]
    public async Task StageChangeCannotSilentlySkipExistingQuantityPostingWhenCompanyIsRemoved()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var settled = await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id, fixture.Project.ProjectNumber, fixture.Project.Name, fixture.Project.ParentProjectName,
                fixture.Project.GeneralContractorName, fixture.Project.GeneralContractorContact, fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId, fixture.Project.DepartmentId, fixture.Project.BranchId,
                ProjectStage.PartiallySettled, fixture.Project.AffiliationType, [fixture.LegalEntity.Id],
                fixture.Project.ConcurrencyStamp, "项目进入部分结算"),
            CancellationToken.None);

        var action = () => fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id, fixture.Project.ProjectNumber, fixture.Project.Name, fixture.Project.ParentProjectName,
                fixture.Project.GeneralContractorName, fixture.Project.GeneralContractorContact, fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId, fixture.Project.DepartmentId, fixture.Project.BranchId,
                ProjectStage.UnderConstruction, fixture.Project.AffiliationType, [],
                settled.Overview.ConcurrencyStamp, "移除公司并退回施工阶段"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*签约公司*");
        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.Projects.SingleAsync()).Stage.Should().Be(ProjectStage.PartiallySettled);
        (await fixture.Db.FinanceSettlements.SingleAsync(item => item.SourceType == LedgerSourceType.ProjectQuantity)).SettlementState.Should().Be(LedgerSettlementState.Final);
    }

    [Fact]
    public async Task UpdateCanAddNewProjectTaxConfigurations()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();

        var updated = await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id,
                fixture.Project.ProjectNumber,
                fixture.Project.Name,
                fixture.Project.ParentProjectName,
                fixture.Project.GeneralContractorName,
                fixture.Project.GeneralContractorContact,
                fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId,
                fixture.Project.DepartmentId,
                fixture.Project.BranchId,
                fixture.Project.Stage,
                fixture.Project.AffiliationType,
                [fixture.LegalEntity.Id],
                fixture.Project.ConcurrencyStamp,
                "新增税金配置",
                ContractSigningStatus: fixture.Project.ContractSigningStatus,
                TaxConfigurations:
                [
                    new ProjectTaxConfigurationInput(0.03m, ProjectInvoiceType.Special),
                    new ProjectTaxConfigurationInput(0.09m, ProjectInvoiceType.Ordinary)
                ]),
            CancellationToken.None);

        updated.Overview.TaxConfigurations.Should().NotBeNull();
        updated.Overview.TaxConfigurations!.Should().HaveCount(2).And.OnlyContain(item => item.IsActive);
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
            [fixture.LegalEntity.Id],
            Guid.NewGuid(),
            "并发测试");

        var action = () => fixture.Service.UpdateAsync(new ProjectWorkspaceActor("workspace-user", "项目管理员"), request, CancellationToken.None);

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>().WithMessage("*刷新后重试*");
    }

    [Fact]
    public async Task UpdatePersistsActualProjectDatesAndIncludesThemInAuditSnapshot()
    {
        await using var fixture = await ProjectWorkspaceFixture.CreateAsync();
        var actualStartDate = new DateOnly(2026, 7, 8);
        var actualCompletionDate = new DateOnly(2026, 7, 16);

        var updated = await fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            new UpdateProjectRequest(
                fixture.Project.Id,
                fixture.Project.ProjectNumber,
                fixture.Project.Name,
                null,
                fixture.Project.GeneralContractorName,
                fixture.Project.GeneralContractorContact,
                fixture.Project.GeneralContractorPhone,
                fixture.Project.ResponsibleUserId,
                fixture.Project.DepartmentId,
                fixture.Project.BranchId,
                fixture.Project.Stage,
                fixture.Project.AffiliationType,
                [fixture.LegalEntity.Id],
                fixture.Project.ConcurrencyStamp,
                "补录实际工期",
                actualStartDate,
                actualCompletionDate),
            CancellationToken.None);

        updated.Overview.ActualStartDate.Should().Be(actualStartDate);
        updated.Overview.ActualCompletionDate.Should().Be(actualCompletionDate);
        var project = await fixture.Db.Projects.AsNoTracking().SingleAsync(item => item.Id == fixture.Project.Id);
        project.ActualStartDate.Should().Be(actualStartDate);
        project.ActualCompletionDate.Should().Be(actualCompletionDate);
        var audit = await fixture.Db.AuditLogs.AsNoTracking().SingleAsync(item => item.Action == "UpdateProject");
        audit.AfterJson.Should().Contain("\"ActualStartDate\":\"2026-07-08\"");
        audit.AfterJson.Should().Contain("\"ActualCompletionDate\":\"2026-07-16\"");
    }

    [Fact]
    public async Task UpdateRejectsActualCompletionBeforeActualStart()
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
            [fixture.LegalEntity.Id],
            fixture.Project.ConcurrencyStamp,
            "错误工期",
            new DateOnly(2026, 7, 10),
            new DateOnly(2026, 7, 9));

        var action = () => fixture.Service.UpdateAsync(
            new ProjectWorkspaceActor("workspace-user", "项目管理员"),
            request,
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*完工日期不得早于开工日期*");
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
            var receivable = new FinanceSettlement
            {
                Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = LedgerSettlementState.Final,
                SourceType = LedgerSourceType.CentralLedger, Project = Project, Contract = contract, LegalEntity = LegalEntity,
                BusinessPartner = partner, BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 100m,
                OriginalInvoiceAmount = 100m, Notes = "进度应收"
            };
            var payable = new FinanceSettlement
            {
                Scope = LedgerScope.External, Direction = LedgerDirection.Payable, SettlementState = LedgerSettlementState.Final,
                SourceType = LedgerSourceType.CentralLedger, Project = Project, Contract = contract, LegalEntity = LegalEntity,
                BusinessPartner = partner, BusinessDate = new DateOnly(2026, 7, 2), OriginalAmount = 80m,
                OriginalInvoiceAmount = 80m, Notes = "班组应付"
            };
            var collection = new FinanceCashEntry
            {
                Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, CashType = LedgerCashType.Collection,
                LegalEntity = LegalEntity, BusinessPartner = partner, Account = account, BusinessDate = new DateOnly(2026, 7, 5),
                Amount = 40m, Notes = "首笔到账"
            };
            collection.Allocations.Add(new FinanceCashAllocation { CashEntry = collection, Settlement = receivable, Project = Project, Contract = contract, Amount = 40m, AllocationOrder = 1 });
            var invoice = new FinanceInvoice
            {
                Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, LegalEntity = LegalEntity,
                BusinessPartner = partner, InvoiceNumber = "INV-WORK-01", InvoiceDate = new DateOnly(2026, 7, 6),
                NetAmount = 28.30m, TaxAmount = 1.70m, Amount = 30m
            };
            invoice.Allocations.Add(new FinanceInvoiceAllocation { Invoice = invoice, Settlement = receivable, Project = Project, Contract = contract, Amount = 30m, AllocationOrder = 1 });
            var payment = new FinanceCashEntry
            {
                Scope = LedgerScope.External, Direction = LedgerDirection.Payable, CashType = LedgerCashType.Payment,
                LegalEntity = LegalEntity, BusinessPartner = partner, Account = account, BusinessDate = new DateOnly(2026, 7, 7),
                Amount = 25m, Notes = "首笔付款"
            };
            payment.Allocations.Add(new FinanceCashAllocation { CashEntry = payment, Settlement = payable, Project = Project, Contract = contract, Amount = 25m, AllocationOrder = 1 });
            Db.AddRange(
                LegalEntity,
                SecondLegalEntity,
                partner,
                Project,
                account,
                receivable,
                payable,
                collection,
                invoice,
                payment);
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
