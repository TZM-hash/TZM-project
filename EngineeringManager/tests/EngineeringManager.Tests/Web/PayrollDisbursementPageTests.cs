using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class PayrollDisbursementPageTests
{
    [Fact]
    public void PayrollPagesExposeBatchListMixedLinesDifferenceAndSourceLocation()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Index.cshtml");
        var edit = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Edit.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Edit.cshtml.cs");

        index.Should().Contain("工资台账");
        index.Should().Contain("/Payroll/Edit");
        edit.Should().Contain("实际发放总金额");
        edit.Should().Contain("自有员工");
        edit.Should().Contain("施工班组人员");
        edit.Should().Contain("临时人员");
        edit.Should().Contain("批次差额");
        edit.Should().Contain("修改原因");
        model.Should().Contain("LineId");
        edit.Should().Contain("data-payroll-line-id");
    }

    [Fact]
    public void SidebarRestoresPayrollAndAddsCrewAndTemporaryWorkerEntries()
    {
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");

        layout.Should().Contain("asp-page=\"/Payroll/Index\"");
        layout.Should().Contain("asp-page=\"/Crews/Index\"");
        layout.Should().Contain("asp-page=\"/TemporaryWorkers/Index\"");
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

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
