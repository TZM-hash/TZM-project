using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class SimpleXlsxWorkbook
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private readonly List<WorksheetData> worksheets = [];

    public void AddWorksheet(string name, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        ValidateWorksheetName(name);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        if (headers.Count == 0 || headers.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("工作表必须包含非空表头。", nameof(headers));
        }

        if (worksheets.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"工作表名称重复：{name}", nameof(name));
        }

        var materializedRows = rows.Select(row => (IReadOnlyList<object?>)row.ToArray()).ToArray();
        if (materializedRows.Any(row => row.Count > headers.Count))
        {
            throw new ArgumentException("数据列数不能超过表头列数。", nameof(rows));
        }

        worksheets.Add(new WorksheetData(name, headers.ToArray(), materializedRows));
    }

    public byte[] ToArray()
    {
        if (worksheets.Count == 0)
        {
            throw new InvalidOperationException("工作簿至少需要一个工作表。");
        }

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            WriteXml(archive, "[Content_Types].xml", CreateContentTypes());
            WriteXml(archive, "_rels/.rels", CreateRootRelationships());
            WriteXml(archive, "xl/workbook.xml", CreateWorkbook());
            WriteXml(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationships());
            for (var index = 0; index < worksheets.Count; index++)
            {
                WriteXml(archive, $"xl/worksheets/sheet{index + 1}.xml", CreateWorksheet(worksheets[index]));
            }
        }

        return output.ToArray();
    }

    private XDocument CreateContentTypes()
    {
        XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        return new XDocument(
            new XElement(contentTypes + "Types",
                new XElement(contentTypes + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(contentTypes + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(contentTypes + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                worksheets.Select((_, index) => new XElement(contentTypes + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{index + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
    }

    private static XDocument CreateRootRelationships()
    {
        XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(new XElement(relationships + "Relationships",
            new XElement(relationships + "Relationship",
                new XAttribute("Id", "rId1"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                new XAttribute("Target", "xl/workbook.xml"))));
    }

    private XDocument CreateWorkbook()
    {
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        return new XDocument(new XElement(SpreadsheetNamespace + "workbook",
            new XAttribute(XNamespace.Xmlns + "r", relationships),
            new XElement(SpreadsheetNamespace + "sheets",
                worksheets.Select((sheet, index) => new XElement(SpreadsheetNamespace + "sheet",
                    new XAttribute("name", sheet.Name),
                    new XAttribute("sheetId", index + 1),
                    new XAttribute(relationships + "id", $"rId{index + 1}"))))));
    }

    private XDocument CreateWorkbookRelationships()
    {
        XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(new XElement(relationships + "Relationships",
            worksheets.Select((_, index) => new XElement(relationships + "Relationship",
                new XAttribute("Id", $"rId{index + 1}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                new XAttribute("Target", $"worksheets/sheet{index + 1}.xml")))));
    }

    private static XDocument CreateWorksheet(WorksheetData worksheet)
    {
        var rows = new List<XElement> { CreateRow(1, worksheet.Headers.Cast<object?>().ToArray()) };
        rows.AddRange(worksheet.Rows.Select((row, index) => CreateRow(index + 2, row)));
        return new XDocument(new XElement(SpreadsheetNamespace + "worksheet",
            new XElement(SpreadsheetNamespace + "sheetData", rows)));
    }

    private static XElement CreateRow(int rowNumber, IReadOnlyList<object?> values) =>
        new(SpreadsheetNamespace + "row",
            new XAttribute("r", rowNumber),
            values.Select((value, columnIndex) => CreateCell(ColumnName(columnIndex + 1) + rowNumber, value)));

    private static XElement CreateCell(string reference, object? value)
    {
        if (value is null)
        {
            return new XElement(SpreadsheetNamespace + "c", new XAttribute("r", reference));
        }

        if (value is bool boolean)
        {
            return new XElement(SpreadsheetNamespace + "c", new XAttribute("r", reference), new XAttribute("t", "b"), new XElement(SpreadsheetNamespace + "v", boolean ? "1" : "0"));
        }

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            return new XElement(SpreadsheetNamespace + "c", new XAttribute("r", reference), new XElement(SpreadsheetNamespace + "v", Convert.ToString(value, CultureInfo.InvariantCulture)));
        }

        var text = value switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
        return new XElement(SpreadsheetNamespace + "c",
            new XAttribute("r", reference),
            new XAttribute("t", "inlineStr"),
            new XElement(SpreadsheetNamespace + "is", new XElement(SpreadsheetNamespace + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
    }

    private static string ColumnName(int columnNumber)
    {
        var name = string.Empty;
        while (columnNumber > 0)
        {
            columnNumber--;
            name = (char)('A' + (columnNumber % 26)) + name;
            columnNumber /= 26;
        }

        return name;
    }

    private static void WriteXml(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        document.Save(writer, SaveOptions.DisableFormatting);
    }

    private static void ValidateWorksheetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 31 || name.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0)
        {
            throw new ArgumentException("工作表名称不能为空、不能超过 31 个字符且不能包含 []:*?/\\。", nameof(name));
        }
    }

    private sealed record WorksheetData(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows);
}
