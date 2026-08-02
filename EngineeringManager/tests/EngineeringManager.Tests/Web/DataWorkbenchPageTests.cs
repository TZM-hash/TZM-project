using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class DataWorkbenchPageTests
{
    [Fact]
    public void DataExchangePageSeparatesExportImportAndHistoryWorkspaces()
    {
        var razor = ReadFile("src", "EngineeringManager.Web", "Pages", "DataExchange", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "DataExchange", "Index.cshtml.cs");

        razor.Should().Contain("data-exchange-export")
            .And.Contain("data-exchange-import")
            .And.Contain("data-exchange-history")
            .And.Contain("IncludeAttachments")
            .And.Contain("SourceMappingJson")
            .And.Contain("下载空白模板")
            .And.Contain("DataExchangeLabels.Dataset")
            .And.Contain("DataExchangeLabels.TaskStatus")
            .And.Contain("data-check-selector")
            .And.Contain("selection-dropdown")
            .And.NotContain("GetEnumSelectList")
            .And.NotContain("_DataWorkbench");
        model.Should().Contain("OnPostExportModulesAsync")
            .And.Contain("ListTasksAsync")
            .And.Contain("CanViewSensitiveData: CanManage")
            .And.Contain("\"ProjectNumber\", true, PageSize: 100");
    }

    [Fact]
    public void FinanceLedgerPageUsesSharedWorkbenchAndCurrentViewExportHandler()
    {
        var razor = ReadFile("src", "EngineeringManager.Web", "Pages", "Finance", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Finance", "Index.cshtml.cs");

        razor.Should().Contain("_DataWorkbench")
            .And.Contain("data-column-key")
            .And.Contain("SelectedFields")
            .And.Contain("asp-page-handler=\"Export\"");
        model.Should().Contain("OnPostExportAsync")
            .And.Contain("OnPostSaveViewAsync")
            .And.Contain("SavedViewId")
            .And.Contain("SortDescending { get; set; } = true")
            .And.Contain("SortOnServer: true")
            .And.Contain("new(\"ProjectNumber\", \"最新项目在前\", true)");
    }

    [Fact]
    public void ProjectLedgerPageUsesSharedWorkbenchAndCompactWorkbookExport()
    {
        var razor = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs");
        var exportPartial = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "components.css");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "check-selector.js");

        razor.Should().Contain("_DataWorkbench")
            .And.Contain("data-column-key")
            .And.NotContain("projects-table-export-form")
            .And.NotContain("project-workbook-export-form\" data-project-export-scope");
        model.Should().Contain("OnPostExportWorkbookAsync")
            .And.Contain("ToolbarActionsPartial: CanExportWorkbook ? \"_ProjectWorkbookExport\" : null")
            .And.NotContain("OnPostExportAsync")
            .And.NotContain("IExportService exportService")
            .And.NotContain("SelectedFields { get; set; }");
        exportPartial.Should().Contain("导出项目清单")
            .And.Contain("project-workbook-export-chevron")
            .And.NotContain("icons.svg#exchange");
        styles.Should().Contain("bottom: calc(100% + .4rem)")
            .And.Contain("var(--project-export-max-height")
            .And.Contain(".project-workbook-export-menu.project-export-opens-down")
            .And.Contain(".data-workbench-toolbar { position: relative; z-index: 2;")
            .And.Contain("body.project-export-open .app-main")
            .And.Contain(".project-workbook-export-popover { position: fixed")
            .And.Contain("max-height: min(78vh, 32rem)");
        script.Should().Contain("project-export-open")
            .And.Contain("project-export-opens-down")
            .And.Contain("--project-export-max-height")
            .And.Contain("getBoundingClientRect")
            .And.Contain("addEventListener(\"toggle\"");
    }

    [Fact]
    public void PaginatedMasterListsUseServerSortingWithNewestBusinessNumberByDefault()
    {
        var projects = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs");
        var employees = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Index.cshtml.cs");
        var employeePage = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Index.cshtml");

        projects.Should().Contain("SortDescending { get; set; } = true")
            .And.Contain("SortOnServer: true")
            .And.Contain("new(\"ProjectNumber\", \"最新项目在前\", true)");
        employees.Should().Contain("SortKey { get; set; } = \"EmployeeNumber\"")
            .And.Contain("SortDescending { get; set; } = true")
            .And.Contain("ApplySort(all)");
        employeePage.Should().Contain("CurrentSortKey = Model.SortKey")
            .And.Contain("SortDescending = Model.SortDescending")
            .And.Contain("SortOnServer = true");
    }

    [Fact]
    public void ExistingServerPaginatedPagesKeepSharedPageSizeSelectorContract()
    {
        var partial = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_DataWorkbench.cshtml");

        partial.Should().Contain("data-current-page-size")
            .And.Contain("aria-label=\"每页显示条数\"")
            .And.Contain("new[] { 20, 50, 100 }");
    }

    [Fact]
    public void CompositeMasterCellsExposeBusinessNumbersForDefaultClientSorting()
    {
        var partners = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var crews = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var employeeLedger = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Ledger.cshtml");

        partners.Should().Contain("data-sort-value=\"@partner.PartnerNumber\"");
        crews.Should().Contain("data-sort-value=\"@partner.PartnerNumber\"");
        employeeLedger.Should().Contain("data-sort-value=\"@row.Employee.EmployeeNumber\"");
    }

    [Fact]
    public void IntegratedListColumnMenusEscapeToolbarScrollClippingWhenOpen()
    {
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        styles.Should().Contain(":is(.company-list-panel, .equipment-workspace-list, .partner-workspace-list, .crew-workspace-list, .employee-workspace-list, .company-certificate-workspace-list, .payroll-workspace-list):has(.column-manager-dropdown[open]) { overflow: visible; }")
            .And.Contain(".equipment-list-toolbar--integrated:has(.column-manager-dropdown[open]) { position: relative; z-index: 5; overflow: visible; }")
            .And.Contain(".equipment-list-toolbar--integrated > .data-workbench:has(.column-manager-dropdown[open]) { overflow: visible; }");
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
