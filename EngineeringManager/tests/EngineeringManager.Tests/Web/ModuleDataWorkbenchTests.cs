using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ModuleDataWorkbenchTests
{
    [Theory]
    [InlineData("Employees", "employees-table")]
    [InlineData("Employees/Ledger", "employee-annual-ledger-table")]
    [InlineData("Partners", "partners-table")]
    [InlineData("StageResults", "stage-results-table")]
    [InlineData("Companies", "companies-table")]
    [InlineData("Equipment", "equipment-table")]
    [InlineData("Crews", "crews-table")]
    [InlineData("Payroll", "payroll-disbursement-table")]
    [InlineData("Reminders", "reminders-table")]
    [InlineData("Backups", "backups-table")]
    [InlineData("Admin/Users", "users-table")]
    [InlineData("Admin/Organizations", "organizations-table")]
    public void MajorListUsesSharedWorkbench(string module, string tableId)
    {
        var html = ReadModuleFile(module);

        html.Should().Contain("_DataWorkbench")
            .And.Contain($"id=\"{tableId}\"")
            .And.Contain("data-column-key");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string ReadModuleFile(string module)
    {
        var parts = module.Split('/');
        var fileName = parts.Length == 1 ? "Index.cshtml" : $"{parts[^1]}.cshtml";
        return File.ReadAllText(Path.Combine(new[] { RepositoryRoot(), "src", "EngineeringManager.Web", "Pages" }.Concat(parts.Take(parts.Length - (parts.Length == 1 ? 0 : 1))).Append(fileName).ToArray()));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
