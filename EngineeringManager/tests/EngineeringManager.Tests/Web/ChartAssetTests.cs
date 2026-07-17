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

        js.Should().Contain("renderLineChart")
            .And.Contain("renderGroupedBars")
            .And.Contain("renderProgressRing")
            .And.Contain("ResizeObserver")
            .And.Contain("prefers-reduced-motion");
        partial.Should().Contain("data-chart-empty");
        layout.Should().NotContain("cdn").And.NotContain("echarts").And.NotContain("chart.js");
    }

    [Theory]
    [InlineData("Projects")]
    [InlineData("Finance")]
    [InlineData("Employees")]
    [InlineData("Payroll")]
    [InlineData("Companies")]
    [InlineData("Equipment")]
    public void CoreModulesExposeChartData(string module)
    {
        var path = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "Pages", module, "Index.cshtml");
        File.ReadAllText(path).Should().Contain("data-chart");
    }

    private static string ReadJavaScript()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "js");
        return string.Join('\n', Directory.EnumerateFiles(directory, "*.js", SearchOption.AllDirectories).Select(File.ReadAllText));
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
