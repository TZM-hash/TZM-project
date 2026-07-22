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
        page.Should().Contain("历史记录");
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
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Edit.cshtml");

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
