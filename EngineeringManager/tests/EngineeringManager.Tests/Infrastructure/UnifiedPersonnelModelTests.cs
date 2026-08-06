using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class UnifiedPersonnelModelTests
{
    [Fact]
    public void UnifiedPersonnelMigrationUsesSeparateSqlServerOwnerFilters()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=EngineeringManager_MigrationScript_Test;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new ApplicationDbContext(options);

        var script = db.GetService<IMigrator>().GenerateScript(
            "20260804065251_ProjectResponsibleEmployeeLinks",
            "20260806071258_UnifiedPersonnelAndOrganizationOwnership");

        script.Should().Contain("IX_OrganizationUnits_LegalEntityId_Code")
            .And.Contain("WHERE [LegalEntityId] IS NOT NULL")
            .And.Contain("IX_OrganizationUnits_BusinessPartnerId_Code")
            .And.Contain("WHERE [BusinessPartnerId] IS NOT NULL")
            .And.NotContain("WHERE [LegalEntityId] IS NOT NULL OR [BusinessPartnerId] IS NOT NULL");
    }

    [Fact]
    public async Task PersonIdentityNumberIsUniqueWhenPresent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.People.AddRange(
            new Person { PersonNumber = "P-001", Name = "甲", IdentityNumber = "110101199001010011", IdentityNumberNormalized = "110101199001010011" },
            new Person { PersonNumber = "P-002", Name = "乙", IdentityNumber = "110101199001010011", IdentityNumberNormalized = "110101199001010011" });

        var action = () => db.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task EmployeeAndConstructionWorkerCanShareOnePersonBridge()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var person = new Person { PersonNumber = "P-BRIDGE", Name = "统一人员" };
        var employee = new Employee { EmployeeNumber = "E-BRIDGE", Name = "统一人员", EmployeeType = EmployeeType.Formal, Person = person };
        var worker = new ConstructionWorker { Name = "统一人员", Person = person };
        db.AddRange(person, employee, worker);

        await db.SaveChangesAsync();

        employee.PersonId.Should().Be(person.Id);
        worker.PersonId.Should().Be(person.Id);
        (await db.People.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EngagementHistoryCanStoreScopeAndEffectiveOwnership()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var person = new Person { PersonNumber = "P-HISTORY", Name = "历史人员" };
        var company = new LegalEntity { Code = "LE-HISTORY", Name = "历史公司", ShortName = "历史公司" };
        db.AddRange(person, company);
        await db.SaveChangesAsync();
        db.PersonnelEngagementHistories.Add(new PersonnelEngagementHistory
        {
            PersonId = person.Id,
            Scope = PersonnelScope.Internal,
            InternalType = EmployeeType.Formal,
            LegalEntityId = company.Id,
            StartDate = new DateOnly(2026, 1, 1),
            IsPrimary = true
        });

        await db.SaveChangesAsync();

        (await db.PersonnelEngagementHistories.SingleAsync()).LegalEntityId.Should().Be(company.Id);
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
}
