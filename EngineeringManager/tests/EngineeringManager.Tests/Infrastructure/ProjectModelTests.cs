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
            Stage = ProjectStage.UnderConstruction,
            ArchiveStatus = ArchiveStatus.NotArchived
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
            EstimatedQuantity = 10m,
            EstimatedUnitPrice = 5m
        });
        project.Contracts.Add(contract);

        db.Projects.Add(project);
        await db.SaveChangesAsync();

        (await db.Projects.CountAsync()).Should().Be(1);
        (await db.Contracts.SingleAsync()).TotalAmount.Should().Be(100m);
        (await db.ContractLineItems.SingleAsync()).EstimatedQuantity.Should().Be(10m);
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

    private static ApplicationDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
}
