using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ListPaginationAssetTests
{
    [Fact]
    public void SharedPaginationAssetSupportsIndependentTablesAndPageSizes()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "list-pagination.js");

        script.Should().Contain("table.data-table:not(.sr-only):not([data-list-pagination-disabled])")
            .And.Contain("20, 50, 100")
            .And.Contain("engineering-manager-list-pagination")
            .And.Contain("data-current-page-size")
            .And.Contain("data-list-pagination-server")
            .And.Contain("MutationObserver")
            .And.Contain("let scanScheduled = false")
            .And.Contain("if (scanScheduled) return")
            .And.Contain("首页")
            .And.Contain("末页")
            .And.Contain("跳转")
            .And.Contain("pagination-page-jump")
            .And.Contain("aria-label", "分页控件必须可访问");
    }

    [Fact]
    public void SiteLoadsPaginationForWorkbenchAndStandaloneTables()
    {
        var site = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

        site.Should().Contain("./components/list-pagination.js")
            .And.Contain("initListPagination");
    }

    [Fact]
    public void EverySharedPresetAllowsPageSizeSelection()
    {
        var presets = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "DataWorkbenchPresets.cs");

        presets.Should().Contain("CanChangePageSize: true");
        presets.Should().NotContain("CanChangePageSize: false");
    }

    [Theory]
    [InlineData("Ledger/External/Index.cshtml")]
    [InlineData("Ledger/Internal/Index.cshtml")]
    public void CentralLedgerMainTablesExposeServerPaginationState(string relativePath)
    {
        var razor = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", relativePath.Replace('/', Path.DirectorySeparatorChar)));

        razor.Should().Contain("data-list-pagination-server=\"true\"")
            .And.Contain("data-list-pagination-current-page")
            .And.Contain("data-list-pagination-total-pages")
            .And.Contain("data-list-pagination-page-size");
    }

    [Fact]
    public void ExistingServerPaginatedPagesExposeTheUnifiedPageSizeOptions()
    {
        var partial = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_DataWorkbench.cshtml");
        partial.Should().Contain("每页显示条数").And.Contain("20").And.Contain("50").And.Contain("100");

        foreach (var page in new[] { "Projects", "Employees", "Finance" })
        {
            ReadFile("src", "EngineeringManager.Web", "Pages", page, "Index.cshtml")
                .Should().Contain("_DataWorkbench");
        }
    }

    [Fact]
    public void ServerPageModelsNormalizeUnsupportedPageSizes()
    {
        foreach (var page in new[] { "Projects", "Employees", "Finance", "Ledger/External", "Ledger/Internal" })
        {
            var parts = page.Split('/');
            var modelPath = parts.Length == 1
                ? new[] { "src", "EngineeringManager.Web", "Pages", parts[0], "Index.cshtml.cs" }
                : new[] { "src", "EngineeringManager.Web", "Pages", parts[0], parts[1], "Index.cshtml.cs" };
            ReadFile(modelPath).Should().Contain("is 20 or 50 or 100 ? pageSize : 20");
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
