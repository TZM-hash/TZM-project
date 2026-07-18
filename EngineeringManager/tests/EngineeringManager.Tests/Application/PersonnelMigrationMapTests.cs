using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class PersonnelMigrationMapTests
{
    [Fact]
    public async Task PersonnelMigrationMapPersistsLegacyAndEmployeeIdentifiersAndTimestamp()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var employee = new Employee { EmployeeNumber = "MIGRATION-E-001", Name = "Migration Employee", EmployeeType = EmployeeType.Labor };
        var legacyTemporaryWorkerId = Guid.NewGuid();
        var migratedAt = new DateTimeOffset(2026, 7, 18, 10, 30, 0, TimeSpan.Zero);
        var migrationMap = new PersonnelMigrationMap
        {
            LegacyTemporaryWorkerId = legacyTemporaryWorkerId,
            Employee = employee,
            MigratedAt = migratedAt
        };
        db.PersonnelMigrationMaps.Add(migrationMap);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var persisted = await db.PersonnelMigrationMaps.SingleAsync();
        persisted.LegacyTemporaryWorkerId.Should().Be(legacyTemporaryWorkerId);
        persisted.EmployeeId.Should().Be(employee.Id);
        persisted.MigratedAt.Should().Be(migratedAt);
    }

    [Fact]
    public async Task DuplicateLegacyTemporaryWorkerIdViolatesUniqueIndex()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var employee = new Employee { EmployeeNumber = "MIGRATION-E-002", Name = "Migration Employee", EmployeeType = EmployeeType.Labor };
        var legacyTemporaryWorkerId = Guid.NewGuid();
        db.PersonnelMigrationMaps.Add(new PersonnelMigrationMap
        {
            LegacyTemporaryWorkerId = legacyTemporaryWorkerId,
            Employee = employee
        });
        await db.SaveChangesAsync();
        db.PersonnelMigrationMaps.Add(new PersonnelMigrationMap
        {
            LegacyTemporaryWorkerId = legacyTemporaryWorkerId,
            EmployeeId = employee.Id
        });

        var saveDuplicate = () => db.SaveChangesAsync();

        await saveDuplicate.Should().ThrowAsync<DbUpdateException>();
    }
}
