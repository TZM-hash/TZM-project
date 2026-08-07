using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class PersonnelResponsiveUiTests
{
    [Fact]
    public void PersonnelFiltersUseSingleRowDesktopLayoutWithResponsiveFallback()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var pages = new[]
        {
            ReadFile("src", "EngineeringManager.Web", "Pages", "Personnel", "Internal", "Index.cshtml"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Personnel", "External", "Index.cshtml")
        };

        pages.Should().OnlyContain(page => page.Contains(
            "class=\"panel filter-bar personnel-filter-bar\"",
            StringComparison.Ordinal));
        css.Should().Contain(".personnel-filter-bar {\n  display: grid;")
            .And.Contain("grid-template-columns: minmax(10rem, 1.35fr) repeat(6, minmax(0, 1fr)) auto;")
            .And.Contain(".personnel-filter-bar > .page-actions-inline { flex-wrap: nowrap;")
            .And.Contain("@media (max-width: 1100px)")
            .And.Contain(".personnel-filter-bar { grid-template-columns: repeat(4, minmax(0, 1fr)); }")
            .And.Contain("@media (max-width: 680px)")
            .And.Contain(".personnel-filter-bar { grid-template-columns: 1fr; }");
    }

    [Fact]
    public void PersonnelTablesConstrainLongCellsAndKeepActionsOnOneLine()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        css.Should().Contain(".personnel-workspace-table { width: 100%;")
            .And.Contain("table-layout: fixed")
            .And.Contain(".personnel-table-wrap { min-width: 0; max-width: 100%; overflow-x: auto;")
            .And.Contain(".personnel-number-cell")
            .And.Contain("text-overflow: ellipsis")
            .And.Contain(".personnel-row-actions { display: flex; flex-wrap: nowrap;")
            .And.Contain("white-space: nowrap");
    }

    [Fact]
    public void AffiliationEditorUsesFingerprintedModuleAndAccessibleWrappedLabels()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml");

        page.Should().Contain("src=\"~/js/pages/personnel-affiliation.js\"")
            .And.Contain("asp-append-version=\"true\"")
            .And.Contain("<label><span>当前公司 / 单位</span><select")
            .And.Contain("<label><span>当前部门</span><select")
            .And.Contain("<label><span>当前项目</span><select")
            .And.Contain("<label><span>当前班组</span><select");
    }

    [Fact]
    public void PersonnelAndOrganizationPanelsHaveResponsiveGridRules()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        css.Should().Contain(".organization-summary__metrics { display: grid; grid-template-columns: repeat(auto-fit, minmax(7.4rem, 1fr));")
            .And.Contain("@media (max-width: 1100px)")
            .And.Contain(".organization-summary__groups, .organization-summary--compact .organization-summary__groups { grid-template-columns: 1fr; }")
            .And.Contain("@media (max-width: 680px)")
            .And.Contain(".employee-project-detail-page .personnel-affiliation-readonly { grid-template-columns: minmax(0, 1fr); }")
            .And.Contain(".employee-project-detail-page .personnel-affiliation-actions { align-items: stretch; flex-direction: column; }")
            .And.Contain(".employee-project-detail-page .personnel-affiliation-form :is(input, select) { width: 100%; min-width: 0; }")
            .And.Contain(".personnel-detail-workspace .personnel-affiliation-form")
            .And.Contain(".personnel-detail-workspace .personnel-affiliation-actions");
    }

    [Fact]
    public void OrganizationMetricsHaveVisibleLinkTextAndTablesScrollInsideTheirWrappers()
    {
        var summary = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_OrganizationSummary.cshtml");
        var components = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "components.css");

        summary.Should().Contain("<span>项目总数</span>")
            .And.Contain("<span>进行中</span>")
            .And.Contain("<span>当前人员</span>")
            .And.Contain("<span>启用 / 总部门</span>");
        components.Should().Contain(".table-wrap, .data-table-wrap")
            .And.Contain("overflow-x: auto");
    }

    [Fact]
    public void PartnerAndCrewTablesKeepResponsiveOverflowInsideTheirWorkspaces()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        css.Should().Contain("@media (max-width: 1280px)")
            .And.Contain(".partner-workspace-layout, .crew-workspace-layout { grid-template-columns: 1fr; }")
            .And.Contain(".partner-name-clamp, .crew-name-clamp")
            .And.Contain("white-space: normal")
            .And.Contain(".partner-cell-ellipsis, .crew-cell-ellipsis")
            .And.Contain("text-overflow: ellipsis")
            .And.Contain(".partner-table-wrap, .crew-table-wrap")
            .And.Contain("max-width: 100%")
            .And.Contain("overflow-x: auto");
    }

    [Fact]
    public void OrganizationListPagesUseBatchSummaryLoading()
    {
        foreach (var pageModel in new[]
        {
            ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Index.cshtml.cs"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml.cs"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml.cs")
        })
        {
            pageModel.Should().Contain("organizationSummaryService.GetManyAsync(");
        }
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
