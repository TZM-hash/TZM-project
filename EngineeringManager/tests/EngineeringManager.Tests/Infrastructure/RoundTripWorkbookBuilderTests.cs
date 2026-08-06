using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Infrastructure.DataExchange;
using FluentAssertions;
using System.IO.Compression;
using System.Xml.Linq;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class RoundTripWorkbookBuilderTests
{
    [Fact]
    public void StandardWorkbookContainsDirectoryDescriptionAndEditableControlColumns()
    {
        var builder = new RoundTripWorkbookBuilder();
        var bytes = builder.Build(new RoundTripWorkbookRequest(
            "export-batch-001",
            [new RoundTripWorkbookSheet(
                ExportDataset.Employees,
                "员工",
                [
                    new ExportFieldDefinition("employee_number", "员工编号", ExportFieldDataType.Text, true),
                    new ExportFieldDefinition("name", "姓名", ExportFieldDataType.Text, true)
                ],
                [new RoundTripWorkbookRow(
                    new Dictionary<string, object?>
                    {
                        ["employee_number"] = "E-001",
                        ["name"] = "张三"
                    },
                    RecordId: "record-001",
                    BusinessKey: "E-001",
                    RowVersion: "7")])],
            DatasetVersion: "employees/1",
            SourcePage: "/Employees"));

        var sheets = SimpleXlsxReader.Read(bytes);
        sheets.Select(sheet => sheet.Name).Should().Equal("目录", "数据说明", "员工");
        sheets[0].Rows[0].Should().Equal("数据集", "工作表", "记录数", "来源页面", "导出批次");
        sheets[1].Rows[0].Should().Contain("字段");

        var dataSheet = sheets.Single(sheet => sheet.Name == "员工");
        dataSheet.Rows[0].Should().ContainInOrder(
            "员工编号", "姓名", "_record_id", "_business_key", "_row_version", "_dataset_key", "_dataset_version", "_export_batch_id");
        dataSheet.Rows[1].Should().ContainInOrder(
            "E-001", "张三", "record-001", "E-001", "7", "Employees", "employees/1", "export-batch-001");

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sheetXml = XDocument.Load(archive.GetEntry("xl/worksheets/sheet3.xml")!.Open());
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        sheetXml.Root!.Element(spreadsheet + "sheetProtection").Should().BeNull();
        sheetXml.Descendants(spreadsheet + "col").Should().HaveCount(6);
        sheetXml.Descendants(spreadsheet + "col").Should().OnlyContain(column => (string?)column.Attribute("hidden") == "1");
    }

    [Fact]
    public void StandardWorkbookLeavesAllSheetsEditable()
    {
        var builder = new RoundTripWorkbookBuilder();
        var bytes = builder.Build(new RoundTripWorkbookRequest(
            "export-batch-editable",
            [new RoundTripWorkbookSheet(
                ExportDataset.Employees,
                "员工",
                [new ExportFieldDefinition("name", "姓名", ExportFieldDataType.Text, true)],
                [new RoundTripWorkbookRow(
                    new Dictionary<string, object?> { ["name"] = "张三" },
                    RecordId: "record-001",
                    BusinessKey: "E-001",
                    RowVersion: "7")])],
            DatasetVersion: "employees/1"));

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        archive.Entries.Where(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal))
            .Select(entry => XDocument.Load(entry.Open()))
            .Should().OnlyContain(document => document.Root!.Element(spreadsheet + "sheetProtection") == null);
    }

    [Fact]
    public void UserFieldOrderIsPreservedAndTechnicalFieldsAreNotDuplicated()
    {
        var builder = new RoundTripWorkbookBuilder();
        var bytes = builder.Build(new RoundTripWorkbookRequest(
            "batch-002",
            [new RoundTripWorkbookSheet(
                ExportDataset.Projects,
                "项目",
                [
                    new ExportFieldDefinition("notes", "备注", ExportFieldDataType.Text, false),
                    new ExportFieldDefinition("_system_id", "旧系统ID", ExportFieldDataType.Text, false, CanImport: false, CanExport: true),
                    new ExportFieldDefinition("project_number", "项目编号", ExportFieldDataType.Text, true)
                ],
                [new RoundTripWorkbookRow(
                    new Dictionary<string, object?>
                    {
                        ["notes"] = "说明",
                        ["_system_id"] = "legacy-id",
                        ["project_number"] = "P-001"
                    },
                    RecordId: "record-002",
                    BusinessKey: "P-001",
                    RowVersion: "8")])],
            DatasetVersion: "projects/1"));

        var sheet = SimpleXlsxReader.Read(bytes).Single(item => item.Name == "项目");
        sheet.Rows[0].Should().Equal("备注", "项目编号", "_record_id", "_business_key", "_row_version", "_dataset_key", "_dataset_version", "_export_batch_id");
        sheet.Rows[1].Should().Equal("说明", "P-001", "record-002", "P-001", "8", "Projects", "projects/1", "batch-002");
    }
}
