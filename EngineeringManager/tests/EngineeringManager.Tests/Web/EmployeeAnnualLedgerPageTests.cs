using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class EmployeeAnnualLedgerPageTests
{
    [Fact]
    public void EmployeeDetailsExposeFiveWorkspaceTabsAndRightActivityRail()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml");

        page.Should().Contain("工资明细");
        page.Should().Contain("报销明细");
        (page.Split("data-employee-main-tab", StringSplitOptions.None).Length - 1).Should().Be(5);
        page.Should().Contain("利息分红");
        page.Should().Contain("付款记录");
        page.Should().Contain("证书管理");
        page.Should().Contain("提示与记录");
        page.Should().Contain("employee-activity-rail");
        page.Should().Contain("证书摘要");
        page.Should().Contain("全部").And.Contain("考勤工资").And.Contain("加班工资").And.Contain("奖金").And.Contain("罚款").And.Contain("其他");
        page.Should().Contain("data-inline-cell-edit");
        page.Should().Contain("asp-page-handler=\"AddWage\"");
        page.Should().Contain("asp-for=\"WageInput.Unit\"");
        page.Should().Contain("asp-page-handler=\"AddExpense\"");
        page.Should().Contain("asp-for=\"ExpenseInput.Attachment\"");
        page.Should().Contain("asp-page-handler=\"AddOtherPayable\"");
        page.Should().Contain("报销总金额");
        page.Should().Contain("报销日期").And.Contain("报销金额").And.Contain("票据号").And.Contain("附件").And.Contain("备注");
        page.Should().NotContain("ExpenseInput.Category");
        page.Should().NotContain("ExpenseInput.AdjustmentAmount");
        page.Should().NotContain("asp-page-handler=\"AddReceipt\"");
        page.Should().NotContain("ReceiptInput");
    }

    [Fact]
    public void EmployeeDetailsMatchProjectWorkspaceHeaderMetricsAndAutomaticProfileEdit()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml.cs");

        page.Should().Contain("employee-project-detail-page")
            .And.NotContain("project-detail-back-row")
            .And.Contain("employee-detail-metric-grid")
            .And.Contain("往年结转")
            .And.Contain("工资应付")
            .And.Contain("罚款扣减")
            .And.Contain("报销应付")
            .And.Contain("利息分红及其他应付")
            .And.Contain("本年新增应付")
            .And.Contain("已付款")
            .And.Contain("当前余额")
            .And.Contain("data-inline-edit-active")
            .And.Contain("提示与记录");

        model.Should().Contain("public string? Edit")
            .And.Contain("Edit == \"profile\"");
    }

    [Fact]
    public void EmployeeDetailsExposeAdjacentEmployeeNavigation()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml.cs");

        page.Should().Contain("employee-detail-navigation")
            .And.Contain("上一页")
            .And.Contain("下一页")
            .And.Contain("Model.PreviousEmployeeId")
            .And.Contain("Model.NextEmployeeId")
            .And.Contain("asp-route-businessYearId")
            .And.Contain("asp-route-wageSubtab")
            .And.Contain("asp-route-paymentSubtab")
            .And.Contain("asp-route-dividendSubtab")
            .And.Contain("asp-route-edit");
        model.Should().Contain("EmployeeNavigationResolver.Resolve")
            .And.Contain("OrderByDescending");
    }

    [Fact]
    public void EmployeeProfileEditUsesTheDetailGridWithoutDuplicateBackNavigation()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().NotContain("project-detail-back-row")
            .And.Contain("employee-detail-layout")
            .And.Contain("employee-profile-panel")
            .And.Contain("project-activity-panel")
            .And.NotContain("visually-hidden\">历史记录");
        page.IndexOf("employee-profile-panel", StringComparison.Ordinal)
            .Should().BeLessThan(page.IndexOf("employee-main-tabs", StringComparison.Ordinal));

        styles.Should().Contain(".employee-project-detail-page .employee-profile-panel [data-inline-edit-control].inline-cell-control:not([hidden])")
            .And.Contain("position: static")
            .And.Contain(".employee-project-detail-page .employee-profile-grid { gap: 0;");
    }

    [Fact]
    public void EmployeeSummaryAndViewDialogUseSemanticMetricColors()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Index.cshtml");
        var dialog = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "_EmployeeEditor.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        index.Should().Contain("employee-summary-metric--total")
            .And.Contain("employee-summary-metric--active")
            .And.Contain("employee-summary-metric--payable")
            .And.Contain("employee-summary-metric--unpaid");
        dialog.Should().Contain("employee-dialog-metric--wage")
            .And.Contain("employee-dialog-metric--penalty")
            .And.Contain("employee-dialog-metric--balance");
        styles.Should().Contain(".employee-summary-metric--total")
            .And.Contain(".employee-dialog-metric--wage")
            .And.Contain(".employee-detail-metric-grid .employee-metric--balance");
    }

    [Fact]
    public void EmployeeListLedgerColumnsUseTheSameSemanticAmountColorsAsDetails()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Index.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        index.Should().Contain("data-column-key=\"payable\"")
            .And.Contain("data-column-key=\"paid\"")
            .And.Contain("data-column-key=\"unpaid\"");
        styles.Should().Contain(".employee-workspace-table td[data-column-key=\"payable\"]")
            .And.Contain(".employee-workspace-table td[data-column-key=\"paid\"]")
            .And.Contain(".employee-workspace-table td[data-column-key=\"unpaid\"]");
    }

    [Fact]
    public void ActivityRailsStretchToTheWorkspaceBottomAndScrollTheirOwnRecords()
    {
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        styles.Should().Contain(".project-workspace-shell { display: grid; grid-template-columns: minmax(0, 1fr) minmax(260px, 320px); gap: var(--space-4); align-items: stretch; }")
            .And.Contain(".project-activity-panel { position: sticky; top: calc(var(--header-height) + var(--space-3)); display: grid; grid-template-rows: auto minmax(0, 1fr); align-self: stretch; height: 100%; min-height: 0; max-height: none;")
            .And.Contain(".project-activity-panel .activity-feed { min-height: 0; max-height: none; overflow-y: auto;")
            .And.Contain(".employee-project-detail-page .employee-activity-rail { top: calc(var(--header-height) + var(--space-3)); align-self: stretch; height: 100%; max-height: none; }");
    }

    [Fact]
    public void EmployeeLedgerAndCertificatesReuseTheEmployeeManagementWorkspaceWithoutDuplicateHeaderLinks()
    {
        var employeeIndex = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Index.cshtml");
        var ledger = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Ledger.cshtml");
        var certificates = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Certificates", "Index.cshtml");
        var certificateModel = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Certificates", "Index.cshtml.cs");

        employeeIndex.Should().NotContain("asp-page=\"/Employees/Ledger\"")
            .And.NotContain("asp-page=\"/Employees/Certificates\"");
        ledger.Should().Contain("employee-workspace-page")
            .And.Contain("employee-workspace-layout")
            .And.Contain("employee-workspace-summary")
            .And.Contain("employee-workspace-list")
            .And.Contain("employee-workspace-table");
        certificates.Should().Contain("employee-workspace-page")
            .And.Contain("employee-workspace-layout")
            .And.Contain("employee-workspace-summary")
            .And.Contain("employee-workspace-list")
            .And.Contain("employee-type-summary-row")
            .And.Contain("asp-route-state");
        certificateModel.Should().Contain("StateSummaryCertificates");
    }

    [Fact]
    public void EmployeeOverviewTablesUseSemanticAmountsAndDialogBasedRowActions()
    {
        var employeeIndex = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Index.cshtml");
        var ledger = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Ledger.cshtml");
        var certificates = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Certificates", "Index.cshtml");
        var styles = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var employeeScript = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "employee-workspace.js");
        var certificateScript = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "employee-certificate-workspace.js");

        employeeIndex.Should().Contain("employeeNumber = item.EmployeeNumber")
            .And.Contain("name = item.Name");
        ledger.Should().Contain("data-employee-dialog-open=\"details\"")
            .And.Contain("data-employee-workspace")
            .And.Contain("data-employee-payload")
            .And.Contain("employeeNumber = row.Employee.EmployeeNumber")
            .And.Contain("name = row.Employee.Name")
            .And.Contain("_EmployeeReadOnlyDetails")
            .And.Contain("data-column-key=\"carry_forward\" class=\"numeric-cell ledger-amount--carry\"")
            .And.Contain("data-column-key=\"new_payable\" class=\"numeric-cell ledger-amount--payable\"")
            .And.Contain("data-column-key=\"received\" class=\"numeric-cell ledger-amount--paid\"")
            .And.Contain("ledger-amount--balance");
        certificates.Should().Contain("data-certificate-dialog-open=\"view\"")
            .And.Contain("data-certificate-dialog-open=\"edit\"")
            .And.Contain("data-certificate-dialog-open=\"delete\"")
            .And.Contain("certificateType = item.CertificateType")
            .And.Contain("employeeName = item.EmployeeName")
            .And.Contain("employee-certificate-authority")
            .And.Contain("employee-certificate-workspace.js");
        styles.Should().Contain(".employee-ledger-table .employee-row-actions { grid-template-columns: minmax(0, 1fr); }")
            .And.Contain(".ledger-amount--payable")
            .And.Contain(".employee-certificate-table { width: 100%; min-width: 0; table-layout: fixed; }")
            .And.Contain(".employee-certificate-table th[data-column-key=\"actions\"] { width: 15%; }")
            .And.NotContain(".employee-certificate-table { min-width: 95rem; }")
            .And.Contain(".employee-certificate-authority { display: block;")
            .And.Contain("white-space: normal;");
        employeeScript.Should().Contain("data-employee-details-dialog")
            .And.Contain("employee-dialog-metric--balance")
            .And.Contain("classList.toggle(\"is-danger\", Boolean(payload.isOverpaid))");
        ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "_EmployeeReadOnlyDetails.cshtml")
            .Should().Contain("data-employee-detail=\"affiliationPosition\"");
        certificateScript.Should().Contain("data-certificate-dialog-open")
            .And.Contain("payload.expiresOn || \"长期有效\"");
    }

    [Fact]
    public void EmployeeLedgerRedirectsButPayrollIsRestoredAsUnifiedDisbursementLedger()
    {
        var payroll = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Index.cshtml.cs");
        var ledger = ReadFile("src", "EngineeringManager.Web", "Pages", "EmployeeLedger", "Index.cshtml.cs");

        payroll.Should().Contain("GetDisbursementOverviewAsync");
        payroll.Should().NotContain("RedirectToPage(\"/Employees/Ledger\")");
        ledger.Should().Contain("RedirectToPage(\"/Employees/Ledger\")");
    }

    [Fact]
    public void SidebarKeepsEmployeeManagementAndRestoresPayrollWhileEmployeesStayOutOfPrivateCache()
    {
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");
        var worker = ReadFile("src", "EngineeringManager.Web", "wwwroot", "service-worker.js");

        layout.Should().Contain("asp-page=\"/Employees/Index\"");
        layout.Should().NotContain("asp-page=\"/Employees/Certificates/Index\"");
        layout.Should().Contain("asp-page=\"/Payroll/Index\"");
        layout.Should().NotContain("asp-page=\"/EmployeeLedger/Index\"");
        worker.Should().Contain("'/Employees'");
    }

    [Fact]
    public void EmployeeReceiptLinesCanTraceBackToExactPayrollBatchLine()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml");

        page.Should().Contain("PayrollBatchId");
        page.Should().Contain("asp-page=\"/Payroll/Edit\"");
        page.Should().Contain("asp-route-lineId");
        page.Should().Contain("查看来源批次");
    }

    [Fact]
    public void PayrollEditorExposesDisbursementFundingAndLineClassificationFields()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "_PayrollEditor.cshtml");

        page.Should().Contain("asp-for=\"Input.DisbursementType\"");
        page.Should().Contain("asp-for=\"Input.FundingSource\"");
        page.Should().Contain("asp-for=\"Input.PersonalAdvanceAccountId\"");
        page.Should().Contain("asp-for=\"Input.RepaysPersonalAdvanceAccountId\"");
        page.Should().Contain("PaymentCategory");
        page.Should().Contain("WageCategory");
        page.Should().Contain("LaborBusinessPartnerId");
        page.Should().Contain("ProjectId");
        page.Should().Contain("data-payroll-dependent-fields");
    }

    [Fact]
    public void PersonalAdvanceAccountEditorCapturesOwnerAndShowsSettlementBreakdown()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Finance", "Accounts.cshtml");

        page.Should().Contain("asp-for=\"OwnerName\"");
        page.Should().Contain("asp-for=\"OwnerEmployeeId\"");
        page.Should().Contain("累计垫付");
        page.Should().Contain("已归还");
        page.Should().Contain("未归还");
        page.Should().Contain("data-personal-account-fields");
    }

    [Fact]
    public void EmployeeWorkspaceAssetsExposeStableLayoutAndDependentFieldHooks()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

        css.Should().Contain(".employee-detail-layout");
        css.Should().Contain(".employee-activity-rail");
        css.Should().Contain(".employee-main-tabs");
        script.Should().Contain("data-payroll-dependent-fields");
        script.Should().Contain("data-personal-account-fields");
        script.Should().Contain("data-line-payment-category");
    }

    [Fact]
    public void BusinessYearAdministrationCapturesCustomStartAndEndDates()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Admin", "BusinessYears", "Index.cshtml");

        page.Should().Contain("业务年度");
        page.Should().Contain("asp-for=\"Input.StartDate\"");
        page.Should().Contain("asp-for=\"Input.EndDate\"");
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
