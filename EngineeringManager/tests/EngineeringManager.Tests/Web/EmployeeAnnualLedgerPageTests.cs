using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class EmployeeAnnualLedgerPageTests
{
    [Fact]
    public void EmployeeDetailsExposeSixLedgerTabsAndCertificateSummary()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Details.cshtml");

        page.Should().Contain("工资明细");
        page.Should().Contain("报销明细");
        page.Should().Contain("分红及其他");
        page.Should().Contain("领款明细");
        page.Should().Contain("证书管理");
        page.Should().Contain("历史记录");
        page.Should().Contain("证书摘要");
        page.Should().Contain("未归属");
        page.Should().Contain("asp-page-handler=\"AddWage\"");
        page.Should().Contain("asp-for=\"WageInput.Unit\"");
        page.Should().Contain("asp-page-handler=\"AddExpense\"");
        page.Should().Contain("asp-for=\"ExpenseInput.Attachment\"");
        page.Should().Contain("asp-page-handler=\"AddOtherPayable\"");
        page.Should().Contain("asp-page-handler=\"AddReceipt\"");
        page.Should().Contain("asp-page-handler=\"AddAdjustment\"");
        page.Should().Contain("asp-page-handler=\"ReverseAdjustment\"");
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
        page.Should().Contain("查看并修改来源批次");
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
