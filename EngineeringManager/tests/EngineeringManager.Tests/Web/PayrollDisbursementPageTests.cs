using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class PayrollDisbursementPageTests
{
    [Fact]
    public void PayrollIndexUsesCrewStyleSinglePageWorkspace()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Index.cshtml");
        var indexModel = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Index.cshtml.cs");
        var editor = ReadFileIfExists("src", "EngineeringManager.Web", "Pages", "Payroll", "_PayrollEditor.cshtml");
        var legacyModel = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Edit.cshtml.cs");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "payroll-workspace.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var workbenchPresets = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "DataWorkbenchPresets.cs");

        index.Should().Contain("data-payroll-workspace")
            .And.Contain("payroll-workspace-layout")
            .And.Contain("\"DisbursementScope\", \"发放主体\"")
            .And.Contain("asp-route-disbursementScope=\"@Model.DisbursementScope\"")
            .And.Contain("data-payroll-dialog-open=\"create\"")
            .And.Contain("data-payroll-dialog-open=\"details\"")
            .And.Contain("data-payroll-dialog-open=\"edit\"")
            .And.Contain("data-payroll-details-dialog")
            .And.Contain("data-payroll-roster-open")
            .And.Contain("data-payroll-roster-category=\"employees\"")
            .And.Contain("data-payroll-roster-category=\"temporaryWorkers\"")
            .And.Contain("data-payroll-roster-category=\"crewWorkers\"")
            .And.Contain("data-payroll-roster-dialog")
            .And.Contain("data-payroll-roster-table")
            .And.Contain("data-payroll-roster-identity")
            .And.Contain("data-payroll-roster-bank-account")
            .And.Contain("data-payroll-roster-bank-name")
            .And.Contain("recipientBreakdown")
            .And.Contain("data-payroll-detail-temporary")
            .And.Contain("data-payroll-category-count")
            .And.Contain("data-column-key=\"temporary\"")
            .And.Contain("data-payroll-editor-dialog")
            .And.Contain("~/js/pages/payroll-workspace.js")
            .And.NotContain("asp-page=\"/Payroll/Edit\">新建工资批次");

        editor.Should().Contain("data-payroll-editor-tab=\"employees\"")
            .And.Contain("data-payroll-editor-tab=\"crews\"")
            .And.Contain("data-payroll-detail-total")
            .And.Contain("data-payroll-difference")
            .And.Contain("保存工资批次");

        indexModel.Should().Contain("OnPostSaveAsync")
            .And.Contain("LoadEditorAsync")
            .And.Contain("PayrollEditorInput")
            .And.Contain("FilterByDisbursementScope")
            .And.Contain("BuildOverview(Batches)");
        legacyModel.Should().Contain("RedirectToPage(\"/Payroll/Index\"");
        script.Should().Contain("[data-payroll-workspace]")
            .And.Contain("[data-payroll-dialog-open]")
            .And.Contain("[data-payroll-roster-dialog]")
            .And.Contain("renderRoster")
            .And.Contain("temporaryWorkers")
            .And.Contain("temporaryCount")
            .And.Contain("temporaryAmount")
            .And.Contain("textContent");
        css.Should().Contain(".payroll-workspace-layout")
            .And.Contain(".payroll-workspace-summary")
            .And.Contain(".payroll-workspace-table")
            .And.Contain(".payroll-workspace-summary, .payroll-workspace-list")
            .And.Contain(".payroll-list-toolbar.equipment-list-toolbar--integrated > .data-workbench")
            .And.Contain(".payroll-recipient-breakdown")
            .And.Contain(".payroll-roster-dialog")
            .And.Contain(".payroll-roster-table th, .payroll-roster-table td")
            .And.Contain("border-right: 1px solid var(--app-border)")
            .And.Contain(".payroll-editor-dialog")
            .And.Contain(".payroll-editor-tabs");
        workbenchPresets.Should().Contain("(\"employee\", \"员工\"), (\"temporary\", \"临时人员\"), (\"crew\", \"班组\")");
    }

    [Fact]
    public void PayrollEditorKeepsTwoEditableSourcesWhileWorkspaceShowsThreeRecipientCategories()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Index.cshtml");
        var editor = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "_PayrollEditor.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Index.cshtml.cs");

        index.Should().Contain("工资台账");
        index.Should().Contain("data-payroll-editor-dialog");
        editor.Should().Contain("实际发放总金额");
        editor.Should().Contain("自有员工");
        editor.Should().Contain("施工班组人员");
        editor.Should().NotContain("Input.TemporaryLines");
        model.Should().NotContain("TemporaryLines");
        model.Should().NotContain("LegacyLines");
        model.Should().Contain("item.RecipientType is PayrollRecipientType.Employee or PayrollRecipientType.CrewWorker");
        editor.Should().NotContain("历史临时人员明细");
        editor.Should().NotContain("Model.LegacyLines");
        editor.Should().NotContain("data-payroll-legacy-total");
        model.Should().Contain("item.EmployeeType.ToChinese()");
        index.Should().Contain("temporaryAmount");
        index.Should().Contain("<th data-column-key=\"temporary\">临时人员</th>");
        editor.Should().Contain("批次差额");
        editor.Should().Contain("修改原因");
        model.Should().Contain("LineId");
        editor.Should().Contain("data-payroll-line-id");
    }

    [Fact]
    public void SidebarKeepsEmployeePayrollAndCrewEntriesWithoutTemporaryWorkerEntryPoint()
    {
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");

        layout.Should().Contain("asp-page=\"/Employees/Index\"");
        layout.Should().Contain("asp-page=\"/Payroll/Index\"");
        layout.Should().Contain("asp-page=\"/Crews/Index\"");
        layout.Should().NotContain("/TemporaryWorkers");
    }

    [Fact]
    public void ProjectPaymentTableLinksPayrollCrewPaymentsBackToSourceBatch()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var service = ReadFile("src", "EngineeringManager.Infrastructure", "Projects", "ProjectWorkspaceService.cs");

        service.Should().Contain("PayrollBatchId");
        service.Should().Contain("民工工资代发");
        page.Should().Contain("row.PayrollBatchId");
        page.Should().Contain("查看来源批次");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));
    private static string ReadFileIfExists(params string[] parts)
    {
        var path = Path.Combine(RepositoryRoot(), Path.Combine(parts));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
