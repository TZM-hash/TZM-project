using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class DataWorkbenchAssetTests
{
    [Fact]
    public void WorkbenchSupportsConfirmedInteractionSet()
    {
        var js = ReadJavaScript();
        var dataTable = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");
        var razor = ReadRazor();
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "components.css");
        var savedViews = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "saved-views.js");
        var filterDrawer = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "filter-drawer.js");

        js.Should().Contain("data-column-key");
        dataTable.Should().Contain("event.target === dialog")
            .And.Contain("dialog.close()");
        js.Should().Contain("data-column-order");
        js.Should().Contain("row-spacing-compact");
        js.Should().Contain("table.classList.remove(...rowSpacingClasses)");
        js.Should().Contain("table.classList.add(`row-spacing-${value}`)");
        js.Should().Contain("data-filter-chip");
        js.Should().Contain("data-saved-view-filter-json");
        js.Should().Contain("data-current-page-size");
        js.Should().Contain("data-check-selector-confirm");
        js.Should().Contain("checkSelectorSnapshot");
        js.Should().Contain("restoreCheckSelectorSnapshot");
        js.Should().Contain("data-confirm-columns");
        js.Should().Contain("columnDraft");
        js.Should().Contain("restoreColumnDraft");

        razor.Should().Contain("data-workbench")
            .And.Contain("data-list-sort-menu")
            .And.Contain("排序")
            .And.Contain("data-column-manager-table")
            .And.Contain("column-manager-dropdown")
            .And.Contain("data-show-all-columns")
            .And.Contain("data-confirm-columns")
            .And.Contain("调整后点击确认生效")
            .And.NotContain("column-manager-dialog")
            .And.Contain("data-filter-drawer")
            .And.Contain("filter-dialog")
            .And.Contain("data-save-view-dialog")
            .And.Contain("name=\"sortKey\"")
            .And.Contain("name=\"sortDescending\"");

        savedViews.Should().Contain("url.searchParams.set(\"sortKey\"")
            .And.Contain("url.searchParams.set(\"sortDescending\"")
            .And.NotContain("url.searchParams.set(\"sort\"")
            .And.NotContain("url.searchParams.set(\"descending\"");
        filterDrawer.Should().Contain("savedViewId");

        css.Should().Contain(".data-workbench-toolbar")
            .And.Contain(".column-manager-list")
            .And.Contain(".filter-chip-list")
            .And.Contain(".filter-dialog")
            .And.Contain("max-height: min(85dvh, 56rem)")
            .And.Contain("grid-template-rows: auto minmax(0, 1fr) auto")
            .And.Contain(".workbench-dialog::backdrop")
            .And.NotContain(".workbench-drawer");
    }

    [Fact]
    public void WorkbenchModulesAreLoadedOnlyWhenWorkbenchExists()
    {
        var site = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

        site.Should().Contain("document.querySelector(\"[data-workbench]\")");
        site.Should().Contain("./components/data-table.js");
        site.Should().Contain("./components/list-sorting.js");
        site.Should().Contain("./components/saved-views.js");
        site.Should().Contain("./components/filter-drawer.js");
    }

    [Fact]
    public void WorkbenchPrioritizesLocalColumnsUnlessAViewWasExplicitlySelected()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");
        var razor = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_DataWorkbench.cshtml");

        script.Should().Contain("hasExplicitSavedView")
            .And.Contain("persistAfterInit")
            .And.Contain("localColumns");
        razor.Should().Contain("data-current-saved-view-id=\"@Model.CurrentSavedViewId\"");
    }

    [Fact]
    public void WorkbenchRestoresLegacyStringColumnSelections()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");

        script.Should().Contain("typeof item === \"string\"")
            .And.Contain("key: item");
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
