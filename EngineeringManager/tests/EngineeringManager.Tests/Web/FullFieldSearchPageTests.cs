using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class FullFieldSearchPageTests
{
    [Theory]
    [InlineData("Employees", "Search")]
    [InlineData("Partners", "Search")]
    [InlineData("Companies", "Search")]
    [InlineData("Equipment", "Keyword")]
    [InlineData("Crews", "Search")]
    [InlineData("Projects", "Search")]
    [InlineData("Payroll", "Search")]
    [InlineData("Finance", "Search")]
    [InlineData("Employees/Certificates", "Search")]
    [InlineData("Companies/Certificates", "Search")]
    public void MainCategoryPagesExposeFullFieldSearch(string pageDirectory, string property)
    {
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", pageDirectory, "Index.cshtml"));
        var pageModelPath = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", pageDirectory, "Index.cshtml.cs");
        var pageModel = File.Exists(pageModelPath) ? File.ReadAllText(pageModelPath) : string.Empty;

        page.Should().Contain("_DataWorkbench")
            .And.NotContain("compact-search-bar");
        (page + pageModel).Should().Contain(property)
            .And.Contain("InlineFilters");
    }

    [Fact]
    public void SharedWorkbenchKeepsSearchAndViewToolsOnOneToolbarRow()
    {
        var workbench = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", "Shared", "_DataWorkbench.cshtml"));

        workbench.Should().Contain("workbench-inline-filters")
            .And.Contain("workbench-toolbar-actions")
            .And.Contain("workbench-view-picker")
            .And.Contain("data-open-column-manager")
            .And.Contain("data-row-spacing")
            .And.Contain("data-inline-filter-preserve");
        workbench.IndexOf("workbench-inline-filters", StringComparison.Ordinal)
            .Should().BeLessThan(workbench.IndexOf("workbench-toolbar-actions", StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
