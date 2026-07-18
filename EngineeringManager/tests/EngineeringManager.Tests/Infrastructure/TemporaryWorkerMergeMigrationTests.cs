using FluentAssertions;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class TemporaryWorkerMergeMigrationTests
{
    [Fact]
    public void MigrationHasAtomicPreflightGuardsForEveryKnownConflict()
    {
        var source = ReadMigration();

        source.Should().Contain("Temporary worker identity conflicts with an existing employee");
        source.Should().Contain("Duplicate unconverted temporary worker identities");
        source.Should().Contain("Generated temporary employee number conflicts with an existing employee");
        source.Should().Contain("Temporary worker merge would duplicate a payroll recipient in one batch");
        source.Should().Contain("THROW 51001");
        source.Should().Contain("THROW 51002");
        source.Should().Contain("THROW 51003");
        source.Should().Contain("THROW 51004");
    }

    [Fact]
    public void MigrationMapsPeopleAndPayrollBeforeDroppingLegacySchema()
    {
        var source = ReadMigration();

        source.Should().Contain("TMP-' + REPLACE(CONVERT(nvarchar(36), [tw].[Id]), '-', '')");
        source.Should().Contain("[EmployeeType]");
        source.Should().Contain("CAST(3 AS int)");
        source.Should().Contain("INSERT INTO [PersonnelMigrationMaps]");
        source.Should().Contain("INSERT INTO [EmployeeAffiliationHistories]");
        source.Should().Contain("[RecipientType] = 1,");
        source.Should().Contain("WHEN [payment].[RecipientType] = 1");
        source.Should().Contain("[RecipientKey] = 'employee:' + REPLACE(CONVERT(nvarchar(36), [map].[EmployeeId]), '-', '')");
        source.Should().Contain("Every temporary worker must have a personnel migration map");
        source.Should().Contain("Payroll payments still reference temporary workers after migration");

        var mapIndex = source.IndexOf("INSERT INTO [PersonnelMigrationMaps]", StringComparison.Ordinal);
        var payrollIndex = source.IndexOf("[RecipientType] = 1,", StringComparison.Ordinal);
        var dropColumnIndex = source.IndexOf("name: \"TemporaryWorkerId\"", StringComparison.Ordinal);
        var dropTableIndex = source.IndexOf("name: \"TemporaryWorkers\"", StringComparison.Ordinal);

        mapIndex.Should().BeGreaterThan(-1);
        payrollIndex.Should().BeGreaterThan(mapIndex);
        dropColumnIndex.Should().BeGreaterThan(payrollIndex);
        dropTableIndex.Should().BeGreaterThan(dropColumnIndex);
    }

    private static string ReadMigration()
    {
        var migrationDirectory = Path.Combine(
            RepositoryRoot(),
            "src",
            "EngineeringManager.Infrastructure",
            "Data",
            "Migrations");
        var files = Directory.GetFiles(migrationDirectory, "*MergeTemporaryWorkersIntoEmployees.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        files.Should().ContainSingle("The temporary-worker merge migration must exist exactly once.");
        return File.ReadAllText(files[0]);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
