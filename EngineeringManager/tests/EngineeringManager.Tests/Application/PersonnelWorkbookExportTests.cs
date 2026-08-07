using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.DataExchange;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class PersonnelWorkbookExportTests
{
    [Fact]
    public async Task PersonnelWorkbookExporterWritesSelectedSalaryValuesAndBlankMissingLedgers()
    {
        var summary = new EmployeeAnnualLedgerSummary(
            PriorYearCarryForward: 100m,
            CurrentYearWagePayable: 500m,
            ExpensePayable: 100m,
            OtherPayable: 50m,
            AdjustmentAmount: 25m,
            CurrentYearNewPayable: 675m,
            ReceivedAmount: 400m,
            CurrentBalance: 375m,
            SettlementProgressPercent: 59.26m,
            IsOverpaid: false,
            ReceiptAllocations: []);
        var rows = new[]
        {
            new PersonnelWorkbookRow(
                Guid.NewGuid(), "P-001", "正式员工", "13800000000", "正式员工", "项目经理", "自有公司", "项目部", "示例项目", "一班组", true, summary, 20m),
            new PersonnelWorkbookRow(
                Guid.NewGuid(), "P-002", "外部人员", null, "施工班组人员", "班组长", "合作单位", null, null, "二班组", true, null, null)
        };

        var file = await new PersonnelWorkbookExporter().ExportAsync(
            new PersonnelWorkbookExportRequest(
                rows,
                ["name", "current_year_new_payable", "received_amount", "current_year_unpaid", "current_balance", "penalty_amount"]),
            CancellationToken.None);

        var sheet = SimpleXlsxReader.Read(file.Content).Single();

        sheet.Rows[0].Should().Equal("姓名", "本年应发合计", "已发合计", "未发合计", "当前余额", "罚款扣减");
        sheet.Rows[1].Should().Equal("正式员工", 675m, 400m, 275m, 375m, 20m);
        sheet.Rows[2].Should().Equal(new object?[] { "外部人员", null, null, null, null, null });
    }

    [Fact]
    public void PersonnelWorkbookExporterWhitelistsSalaryAndPersonnelColumns()
    {
        var source = ReadFile("src", "EngineeringManager.Infrastructure", "DataExchange", "PersonnelWorkbookExporter.cs");

        source.Should().Contain("current_year_new_payable")
            .And.Contain("current_year_unpaid")
            .And.Contain("received_amount")
            .And.Contain("current_balance")
            .And.Contain("settlement_progress")
            .And.Contain("penalty_amount")
            .And.Contain("SimpleXlsxWorkbook")
            .And.Contain("PersonnelWorkbookColumnDefinitions");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
