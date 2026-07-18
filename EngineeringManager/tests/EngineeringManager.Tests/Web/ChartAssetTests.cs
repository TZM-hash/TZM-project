using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ChartAssetTests
{
    [Fact]
    public void ChartsUseLocalAccessibleRenderersAndEmptyState()
    {
        var js = ReadJavaScript();
        var partialPath = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", "Shared", "_ChartEmptyState.cshtml");
        File.Exists(partialPath).Should().BeTrue();
        var partial = File.Exists(partialPath) ? File.ReadAllText(partialPath) : string.Empty;
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");
        var css = ReadCss();

        js.Should().Contain("renderLineChart")
            .And.Contain("renderGroupedBars")
            .And.Contain("renderProgressRing")
            .And.Contain("ResizeObserver")
            .And.Contain("prefers-reduced-motion");
        partial.Should().Contain("data-chart-empty");
        css.Should().Contain(".chart-summary [data-chart-canvas]")
            .And.Contain("width: 8rem");
        layout.Should().NotContain("cdn").And.NotContain("echarts").And.NotContain("chart.js");
    }

    [Theory]
    [InlineData("Finance")]
    [InlineData("Companies")]
    [InlineData("Equipment")]
    public void CoreModulesExposeChartData(string module)
    {
        var path = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", module, "Index.cshtml");
        File.ReadAllText(path).Should().Contain("data-chart");
    }

    [Theory]
    [InlineData("Projects")]
    [InlineData("Employees")]
    public void HighFrequencyModulesUseCompactOverviewInsteadOfLargeCharts(string module)
    {
        var path = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", module, "Index.cshtml");
        var page = File.ReadAllText(path);

        page.Should().Contain("overview-strip")
            .And.NotContain("data-chart");
    }

    [Fact]
    public void EmployeeAnnualLedgerUsesCompactOverviewInsteadOfLegacyPayrollChart()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Ledger.cshtml");

        page.Should().Contain("overview-strip")
            .And.NotContain("data-chart");
    }

    private static string ReadJavaScript()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "js");
        return string.Join('\n', Directory.EnumerateFiles(directory, "*.js", SearchOption.AllDirectories).Select(File.ReadAllText));
    }

    private static string ReadCss()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "css");
        return string.Join('\n', Directory.EnumerateFiles(directory, "*.css", SearchOption.TopDirectoryOnly).Select(File.ReadAllText));
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
