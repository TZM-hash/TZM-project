using EngineeringManager.Infrastructure.DataExchange;
using FluentAssertions;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class SimpleXlsxWorkbookTests
{
    [Fact]
    public void WorkbookSupportsMultipleChineseWorksheetsAndTypedCells()
    {
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "总览汇总",
            ["指标", "金额", "截止日", "有效"],
            [
                ["未收款", 1234.56m, new DateOnly(2026, 7, 16), true],
                ["项目数量", 3, null, false]
            ]);
        workbook.AddWorksheet(
            "项目明细",
            ["项目编号", "项目名称"],
            [["P-001", "中文项目"]]);

        var bytes = workbook.ToArray();
        var sheets = SimpleXlsxReader.Read(bytes);

        bytes.Take(2).Should().Equal((byte)'P', (byte)'K');
        sheets.Select(item => item.Name).Should().Equal("总览汇总", "项目明细");
        sheets[0].Rows[0].Should().Equal("指标", "金额", "截止日", "有效");
        sheets[0].Rows[1][0].Should().Be("未收款");
        sheets[0].Rows[1][1].Should().Be(1234.56m);
        sheets[0].Rows[1][2].Should().Be(new DateOnly(2026, 7, 16));
        sheets[0].Rows[1][3].Should().Be(true);
        sheets[1].Rows[1].Should().Equal("P-001", "中文项目");
    }

    [Fact]
    public void DuplicateOrInvalidWorksheetNamesAreRejected()
    {
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("项目", ["编号"], [["P-1"]]);

        var duplicate = () => workbook.AddWorksheet("项目", ["编号"], [["P-2"]]);
        var invalid = () => workbook.AddWorksheet("项目/明细", ["编号"], [["P-3"]]);

        duplicate.Should().Throw<ArgumentException>();
        invalid.Should().Throw<ArgumentException>();
    }
}
