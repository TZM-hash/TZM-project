using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class ProjectModelTests
{
    [Fact]
    public async Task ProjectContractLineItemsAndCompanyAllocationsCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var branch = new OrganizationUnit { Code = "BR-P2", Name = "项目分支", UnitType = OrganizationUnitType.Branch };
        var department = new OrganizationUnit { Code = "DP-P2", Name = "项目部", UnitType = OrganizationUnitType.Department, Parent = branch };
        var legalOne = new LegalEntity { Code = "LE-P21", Name = "甲工程公司", ShortName = "甲公司" };
        var legalTwo = new LegalEntity { Code = "LE-P22", Name = "乙工程公司", ShortName = "乙公司" };
        var manager = new ApplicationUser { UserName = "project-owner", NormalizedUserName = "PROJECT-OWNER", DisplayName = "项目负责人" };
        var project = new Project
        {
            ProjectNumber = "P-2026-001",
            Name = "阶段二测试项目",
            GeneralContractorName = "测试总包单位",
            ResponsibleUser = manager,
            Department = department,
            Branch = branch,
            Stage = ProjectStage.UnderConstruction
        };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = legalOne, IsPrimary = true });
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = legalTwo });
        project.Assignments.Add(new ProjectAssignment { Project = project, User = manager, AssignmentType = ProjectAssignmentType.Responsible });
        project.Milestones.Add(new ProjectMilestone { Project = project, Name = "计划开工", PlannedDate = new DateOnly(2026, 8, 1), SortOrder = 1 });

        var contract = new Contract
        {
            Project = project,
            ContractNumber = "C-001",
            Name = "总包合同",
            ContractType = ContractType.MainContract,
            AllocationMode = ContractAllocationMode.FixedAmount,
            CounterpartyName = "测试总包单位",
            TotalAmount = 100m
        };
        contract.LegalEntityAllocations.Add(new ContractLegalEntityAllocation { Contract = contract, LegalEntity = legalOne, Amount = 60m });
        contract.LegalEntityAllocations.Add(new ContractLegalEntityAllocation { Contract = contract, LegalEntity = legalTwo, Amount = 40m });
        contract.LineItems.Add(new ContractLineItem
        {
            Contract = contract,
            Code = "001",
            Name = "土方工程",
            Unit = "m³",
            Quantity = 10m,
            UnitPrice = 5m,
            AccountingLabel = "现场暂估",
            RequiresInvoice = false
        });
        project.Contracts.Add(contract);

        db.Projects.Add(project);
        await db.SaveChangesAsync();

        (await db.Projects.CountAsync()).Should().Be(1);
        (await db.Contracts.SingleAsync()).TotalAmount.Should().Be(100m);
        var savedLine = await db.ContractLineItems.SingleAsync();
        savedLine.Quantity.Should().Be(10m);
        savedLine.UnitPrice.Should().Be(5m);
        savedLine.AccountingLabel.Should().Be("现场暂估");
        savedLine.RequiresInvoice.Should().BeFalse();
        (await db.ContractLegalEntityAllocations.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ProjectNumberIsUnique()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        db.Projects.AddRange(
            new Project { ProjectNumber = "DUP-P", Name = "项目一" },
            new Project { ProjectNumber = "DUP-P", Name = "项目二" });

        var action = () => db.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ProjectTaxConfigurationsContractStatusAndEquipmentOverviewFlagCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var project = new Project
        {
            ProjectNumber = "P-TAX-01",
            Name = "税金配置项目",
            Stage = ProjectStage.AwaitingMobilization,
            ContractSigningStatus = ContractSigningStatus.SentForSignature
        };
        project.TaxConfigurations.Add(new ProjectTaxConfiguration
        {
            Project = project,
            TaxRate = 0.03m,
            InvoiceType = ProjectInvoiceType.Special
        });
        project.TaxConfigurations.Add(new ProjectTaxConfiguration
        {
            Project = project,
            TaxRate = 0.09m,
            InvoiceType = ProjectInvoiceType.Ordinary
        });
        var equipment = new Equipment { EquipmentNumber = "EQ-P-TAX", Name = "履带吊" };
        project.ConstructionRecords.Add(new ProjectConstructionRecord
        {
            Project = project,
            RecordType = ProjectConstructionRecordType.Equipment,
            Equipment = equipment,
            ShowInProjectOverview = true
        });

        db.Add(project);
        await db.SaveChangesAsync();

        var saved = await db.Projects.Include(item => item.TaxConfigurations).Include(item => item.ConstructionRecords).SingleAsync();
        saved.ContractSigningStatus.Should().Be(ContractSigningStatus.SentForSignature);
        saved.TaxConfigurations.Should().HaveCount(2);
        saved.ConstructionRecords.Should().ContainSingle(item => item.ShowInProjectOverview);
    }

    [Fact]
    public async Task DuplicateProjectTaxCombinationIsRejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var project = new Project { ProjectNumber = "P-TAX-02", Name = "重复税金项目" };
        project.TaxConfigurations.Add(new ProjectTaxConfiguration { Project = project, TaxRate = 0.03m, InvoiceType = ProjectInvoiceType.Special });
        project.TaxConfigurations.Add(new ProjectTaxConfiguration { Project = project, TaxRate = 0.03m, InvoiceType = ProjectInvoiceType.Special });
        db.Add(project);

        var action = () => db.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
}
