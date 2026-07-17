using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class DataWorkbenchPageTests
{
    [Theory]
    [InlineData("Projects")]
    [InlineData("Finance")]
    public void PrimaryLedgerPageUsesSharedWorkbenchAndExportHandler(string page)
    {
        var razor = ReadFile("src", "EngineeringManager.Web", "Pages", page, "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", page, "Index.cshtml.cs");

        razor.Should().Contain("_DataWorkbench")
            .And.Contain("data-column-key")
            .And.Contain("SelectedFields")
            .And.Contain("asp-page-handler=\"Export\"");
        model.Should().Contain("OnPostExportAsync")
            .And.Contain("OnPostSaveViewAsync")
            .And.Contain("SavedViewId");
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
