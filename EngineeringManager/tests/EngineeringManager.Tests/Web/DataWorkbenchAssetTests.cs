using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class DataWorkbenchAssetTests
{
    [Fact]
    public void WorkbenchSupportsConfirmedInteractionSet()
    {
        var js = ReadJavaScript();
        var razor = ReadRazor();
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "components.css");

        js.Should().Contain("data-column-key");
        js.Should().Contain("data-column-order");
        js.Should().Contain("row-spacing-compact");
        js.Should().Contain("table.classList.remove(...rowSpacingClasses)");
        js.Should().Contain("table.classList.add(`row-spacing-${value}`)");
        js.Should().Contain("data-filter-chip");
        js.Should().Contain("data-saved-view-filter-json");
        js.Should().Contain("data-current-page-size");

        razor.Should().Contain("data-workbench")
            .And.Contain("data-column-manager-table")
            .And.Contain("column-manager-dropdown")
            .And.Contain("data-show-all-columns")
            .And.NotContain("column-manager-dialog")
            .And.Contain("data-filter-drawer")
            .And.Contain("data-save-view-dialog");

        css.Should().Contain(".data-workbench-toolbar")
            .And.Contain(".column-manager-list")
            .And.Contain(".filter-chip-list")
            .And.Contain(".workbench-drawer");
    }

    [Fact]
    public void WorkbenchModulesAreLoadedOnlyWhenWorkbenchExists()
    {
        var site = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

        site.Should().Contain("document.querySelector(\"[data-workbench]\")");
        site.Should().Contain("./components/data-table.js");
        site.Should().Contain("./components/saved-views.js");
        site.Should().Contain("./components/filter-drawer.js");
    }

    private static string ReadJavaScript()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "js");
        return string.Join('\n', Directory.EnumerateFiles(directory, "*.js", SearchOption.AllDirectories).Select(File.ReadAllText));
    }

    private static string ReadRazor()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", "Shared");
        return string.Join('\n', Directory.EnumerateFiles(directory, "*.cshtml", SearchOption.TopDirectoryOnly).Select(File.ReadAllText));
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
