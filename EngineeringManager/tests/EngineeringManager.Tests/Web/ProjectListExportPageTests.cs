using FluentAssertions;
using System.Text.RegularExpressions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectListExportPageTests
{
    [Fact]
    public void ProjectPageUsesIndependentSavedExportColumns()
    {
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs");
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml");
        var export = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml");

        model.Should().Contain("ExportColumns { get; set; }")
            .And.Contain("ExportViewDefinition")
            .And.Contain("projects-export")
            .And.Contain("savedViewService.SaveAsync")
            .And.Contain("ProjectListColumns: ExportColumns")
            .And.Contain("请至少选择一列")
            .And.Contain("ProjectExportColumns => ProjectExportColumnDefinitions")
            .And.Contain("new(\"general_contractor_contact\", \"总包联系人 / 电话\")")
            .And.Contain("new(\"collection_rate\", \"收款率\")")
            .And.Contain("new(\"collection_receivable_amount\", \"应收金额\")")
            .And.Contain("new(\"payment_rate\", \"付款率\")")
            .And.Contain("new(\"invoice_rate\", \"开票率\")")
            .And.NotContain("new(\"general_contractor_phone\", \"总包电话\")");
        page.Should().Contain("form=\"projects-table-workbook-export-form\"");
        export.Should().Contain("name=\"ExportColumns\"")
            .And.Contain("data-check-selector-option")
            .And.Contain("data-check-selector-all")
            .And.Contain("data-check-selector-default")
            .And.Contain("Model.ExportColumns.Contains(column.Key)")
            .And.Contain("@foreach (var column in Model.ProjectExportColumns)")
            .And.Contain("Model.Workbench.Columns")
            .And.Contain("data-export-column-key")
            .And.NotContain("按当前页面筛选、排序和列管理结果生成 Excel");
    }

    [Fact]
    public void IconOnlyDialogCloseButtonsHaveAccessibleNames()
    {
        var root = RepositoryRoot();
        var pages = Path.Combine(root, "src", "EngineeringManager.Web", "Pages");
        var offenders = Directory.EnumerateFiles(pages, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(
                    File.ReadAllText(file),
                    @"<button\b(?<attributes>[^>]*)>\s*×\s*</button>",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)
                .Where(match => !match.Groups["attributes"].Value.Contains("aria-label=", StringComparison.OrdinalIgnoreCase))
                .Select(_ => Path.GetRelativePath(root, file)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("icon-only dialog close buttons need an accessible name");
    }

    [Fact]
    public void ProjectExportCanUseSharedTableColumnSynchronizationForTableMode()
    {
        var export = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");

        export.Should().Contain("data-export-column-key")
            .And.Contain("TableExportColumns")
            .And.Contain("data-project-export-column-source");
        script.Should().Contain("data-export-column-key")
            .And.Contain("exportInputs.sort")
            .And.Contain("exportColumnKey");
    }

    [Fact]
    public void ProjectExportOffersIndependentFiltersAndColumnSourceModes()
    {
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs");
        var export = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "check-selector.js");

        model.Should().Contain("ExportFiltersInitialized")
            .And.Contain("ExportSearch")
            .And.Contain("ExportStages")
            .And.Contain("ExportQuery()")
            .And.Contain("ExportColumnMode")
            .And.Contain("TableExportColumns");
        export.Should().Contain("data-project-export-filter-details")
            .And.Contain("data-project-export-filter")
            .And.Contain("name=\"ExportColumnMode\"")
            .And.Contain("按列管理导出")
            .And.Contain("按内容筛选导出")
            .And.Contain("name=\"TableExportColumns\"");
        script.Should().Contain("data-project-export-filter")
            .And.Contain("data-project-export-filter-count");
    }

    [Fact]
    public void ProjectExportSelectorValidatesAndSavesTheIndependentSelection()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "check-selector.js");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs");

        script.Should().Contain("data-project-export-columns")
            .And.Contain("data-project-export-columns-error")
            .And.Contain("data-check-selector-persist")
            .And.Contain("data-project-export-submit")
            .And.Contain("event.preventDefault()");
        model.Should().Contain("JsonSerializer.Serialize(ExportColumns)")
            .And.Contain("ExportViewName")
            .And.Contain("没有可导出的项目");
    }

    [Fact]
    public void ProjectExportShowsMissingProjectScopeFeedbackBeforeSubmit()
    {
        var export = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "check-selector.js");

        export.Should().Contain("data-project-export-scope-error")
            .And.Contain("请至少勾选一个项目，或选择导出当前筛选命中的全部项目。")
            .And.Contain("hasExportError");
        script.Should().Contain("data-project-export-item")
            .And.Contain("data-project-export-all-matching")
            .And.Contain("data-project-export-scope-error")
            .And.Contain("hasProjectScope")
            .And.Contain("event.preventDefault()");
    }

    [Fact]
    public void ProjectExportValidationRevealsTheErrorInsideTheScrollableMenu()
    {
        var export = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "check-selector.js");

        export.Should().Contain("data-project-export-columns-details");
        script.Should().Contain("scrollIntoView")
            .And.Contain("project-export-columns-details")
            .And.Contain("tabindex");
    }

    [Fact]
    public void ProjectExportDownloadResetsBusyStateAfterFileOrErrorResponse()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "check-selector.js");

        script.Should().Contain("fetch(form.action")
            .And.Contain("response.blob()")
            .And.Contain("Content-Disposition")
            .And.Contain("URL.createObjectURL")
            .And.Contain("URL.revokeObjectURL")
            .And.Contain("const html = await response.text()")
            .And.Contain("document.write(html)")
            .And.Contain("finally")
            .And.Contain("button.disabled = false")
            .And.Contain("生成项目工作簿");
    }

    [Fact]
    public void ProjectExportColumnsAreCollapsedByDefaultAndOpenForColumnErrors()
    {
        var export = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml");

        export.Should().Contain("data-project-export-columns-details")
            .And.Contain("hasExportColumnError")
            .And.Contain("project-workbook-export-columns-body")
            .And.Contain("<summary class=\"project-workbook-export-page-format\">")
            .And.Contain("open=\"@(hasExportColumnError ? \"open\" : null)\"");
    }

    [Fact]
    public void FilterResetAndExportFeedbackKeepTheCurrentPageStateClear()
    {
        var filterDrawer = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "filter-drawer.js");
        var exportPage = ReadFile("src", "EngineeringManager.Web", "Pages", "DataExchange", "Export.cshtml");
        var exportIndex = ReadFile("src", "EngineeringManager.Web", "Pages", "DataExchange", "Index.cshtml");

        filterDrawer.Should().Contain("savedViewId");
        exportPage.Should().Contain("data-project-export-submit");
        exportIndex.Should().Contain("data-project-export-submit");
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
