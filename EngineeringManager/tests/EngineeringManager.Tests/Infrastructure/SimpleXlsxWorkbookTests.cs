using EngineeringManager.Infrastructure.DataExchange;
using FluentAssertions;
using System.Xml.Linq;

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

    [Fact]
    public void WorkbookWritesInternalAndRelativeFileHyperlinks()
    {
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("目录", ["模块", "附件"], [[new XlsxHyperlink("员工", "'员工'!A1"), new XlsxHyperlink("身份证附件", "attachments/employee/id.pdf", true)]]);
        workbook.AddWorksheet("员工", ["姓名"], [["测试员工"]]);

        var bytes = workbook.ToArray();
        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes), System.IO.Compression.ZipArchiveMode.Read);
        using var sheetReader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        using var relationshipReader = new StreamReader(archive.GetEntry("xl/worksheets/_rels/sheet1.xml.rels")!.Open());

        sheetReader.ReadToEnd().Should().Contain("location=\"'员工'!A1\"").And.Contain("r:id=\"rId1\"");
        relationshipReader.ReadToEnd().Should().Contain("attachments/employee/id.pdf");
    }

    [Fact]
    public void WorkbookCanHideTechnicalColumnsAndProtectWorksheet()
    {
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "项目主档",
            ["项目编号", "项目名称", "系统ID", "并发版本"],
            [["P-001", "保护项目", "id", "stamp"]],
            new XlsxWorksheetOptions([2, 3], ProtectSheet: true));

        var bytes = workbook.ToArray();
        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes), System.IO.Compression.ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = reader.ReadToEnd();

        xml.Should().Contain("hidden=\"1\"");
        xml.Should().Contain("sheetProtection");
    }

    [Fact]
    public void ProtectedWorksheetUnlocksBusinessDataCellsAndKeepsTechnicalCellsLocked()
    {
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "Projects",
            ["Project number", "Project name", "System ID"],
            [["P-001", "Editable project", "system-id"]],
            new XlsxWorksheetOptions([2], ProtectSheet: true));

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(workbook.ToArray()), System.IO.Compression.ZipArchiveMode.Read);
        var worksheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var styles = XDocument.Load(archive.GetEntry("xl/styles.xml")!.Open());
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        worksheet.Root!.Element(spreadsheet + "sheetProtection").Should().NotBeNull();
        worksheet.Descendants(spreadsheet + "col").Single(column => (string?)column.Attribute("min") == "3").Attribute("hidden")!.Value.Should().Be("1");
        worksheet.Descendants(spreadsheet + "c").Single(cell => (string?)cell.Attribute("r") == "A2").Attribute("s")!.Value.Should().Be("1");
        worksheet.Descendants(spreadsheet + "c").Single(cell => (string?)cell.Attribute("r") == "B2").Attribute("s")!.Value.Should().Be("1");
        worksheet.Descendants(spreadsheet + "c").Single(cell => (string?)cell.Attribute("r") == "C2").Attribute("s").Should().BeNull();
        styles.Descendants(spreadsheet + "cellXfs").Elements(spreadsheet + "xf").ElementAt(0).Element(spreadsheet + "protection")!.Attribute("locked")!.Value.Should().Be("1");
        styles.Descendants(spreadsheet + "cellXfs").Elements(spreadsheet + "xf").ElementAt(1).Element(spreadsheet + "protection")!.Attribute("locked")!.Value.Should().Be("0");
    }

    [Fact]
    public void ReaderPreservesBlankCellsBeforeLaterTechnicalColumns()
    {
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("项目", ["编号", "名称", "系统ID"], [["P-001", null, "id"]]);

        var row = SimpleXlsxReader.Read(workbook.ToArray())[0].Rows[1];

        row.Should().HaveCount(3);
        row[0].Should().Be("P-001");
        row[1].Should().BeNull();
        row[2].Should().Be("id");
    }
}
