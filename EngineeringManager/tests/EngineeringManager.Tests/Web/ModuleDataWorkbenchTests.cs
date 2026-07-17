using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ModuleDataWorkbenchTests
{
    [Theory]
    [InlineData("Employees", "employees-table")]
    [InlineData("Payroll", "payroll-table")]
    [InlineData("EmployeeLedger", "employee-ledger-table")]
    [InlineData("Partners", "partners-table")]
    [InlineData("StageResults", "stage-results-table")]
    [InlineData("Companies", "companies-table")]
    [InlineData("Equipment", "equipment-table")]
    [InlineData("Reminders", "reminders-table")]
    public void MajorListUsesSharedWorkbench(string module, string tableId)
    {
        var html = ReadFile("src", "EngineeringManager.Web", "Pages", module, "Index.cshtml");

        html.Should().Contain("_DataWorkbench")
            .And.Contain($"id=\"{tableId}\"")
            .And.Contain("data-column-key");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
