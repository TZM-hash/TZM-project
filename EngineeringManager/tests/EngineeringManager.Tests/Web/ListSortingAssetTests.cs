using System.Text.RegularExpressions;
using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ListSortingAssetTests
{
    [Fact]
    public void SharedListSortingEnhancesEveryVisibleDataTable()
    {
        var scriptPath = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "js", "components", "list-sorting.js");
        File.Exists(scriptPath).Should().BeTrue("all list pages need one shared sorting implementation");

        var script = File.ReadAllText(scriptPath);
        script.Should().Contain("table.data-table:not(.sr-only):not([data-list-sort-disabled])")
            .And.Contain("data-list-sort-menu")
            .And.Contain("function workbenchForTable(table)")
            .And.Contain("if (workbenchForTable(table)) return")
            .And.Contain("const enoughRows = businessRows(table).length >= 2")
            .And.Contain("picker.hidden = !enoughRows")
            .And.Contain("if (!table) {")
            .And.Contain("if (picker) picker.hidden = true")
            .And.Contain("const standalonePickers = new WeakMap")
            .And.Contain("standalonePickers.set(table, label)")
            .And.Contain("Intl.Collator")
            .And.Contain("numeric: true")
            .And.Contain("const businessNumberPattern = /编号|编码/;")
            .And.NotContain("编号|编码|序号")
            .And.Contain("function strictNumberValue(value)")
            .And.Contain("const embedded = normalized.match")
            .And.Contain("localStorage")
            .And.Contain("sortKey")
            .And.Contain("sortDescending")
            .And.Contain("MutationObserver")
            .And.Contain("mutation.removedNodes")
            .And.Contain("function rowsMatchOrder(current, expected)")
            .And.Contain("if (!rowsMatchOrder(allRows, orderedRows))")
            .And.Contain("observer?.takeRecords()")
            .And.Contain("if (node.matches(\"tr\") || node.querySelector?.(\"tr\")) return true")
            .And.Contain("let scanScheduled = false")
            .And.Contain("if (scanScheduled) return")
            .And.Contain("data-sort-fixed");

        var pagesRoot = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages");
        var visibleTableCount = Directory.EnumerateFiles(pagesRoot, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "<table\\b[^>]*class=\\\"[^\\\"]*data-table[^\\\"]*\\\"[^>]*>", RegexOptions.IgnoreCase).Cast<Match>())
            .Count(match => !match.Value.Contains("sr-only", StringComparison.OrdinalIgnoreCase));

        visibleTableCount.Should().BeGreaterThanOrEqualTo(70, "the shared enhancer is the coverage gate for all large and small tables");
    }

    [Fact]
    public void SiteLoadsSortingForStandaloneAndWorkbenchTables()
    {
        var site = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

        site.Should().Contain("document.querySelector(\".data-table, [data-workbench]\")")
            .And.Contain("./components/list-sorting.js")
            .And.Contain("initListSorting");
    }

    [Fact]
    public void SortingControlsUseTheExistingApplicationDesignSystem()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "components.css");

        css.Should().Contain(".list-sort-picker")
            .And.Contain(".standalone-list-sort")
            .And.Contain("var(--app-border)")
            .And.Contain("var(--app-surface)");
    }

    [Fact]
    public void ServerSortingClearsLegacyCaseVariantsPageStateAndSavedViewSelection()
    {
        var helperPath = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "js", "components", "url-search-params.js");
        File.Exists(helperPath).Should().BeTrue("all URL-driven list controls need the same case-insensitive cleanup");

        var helper = File.ReadAllText(helperPath);
        var listSorting = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "list-sorting.js");
        var dataTable = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");
        var savedViews = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "saved-views.js");

        helper.Should().Contain("function deleteSearchParamsIgnoreCase")
            .And.Contain("key.toLowerCase()")
            .And.Contain("searchParams.delete(key)");
        foreach (var script in new[] { listSorting, dataTable, savedViews })
        {
            script.Should().Contain("deleteSearchParamsIgnoreCase")
                .And.Contain("pageNumber")
                .And.Contain("savedViewId");
        }
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
