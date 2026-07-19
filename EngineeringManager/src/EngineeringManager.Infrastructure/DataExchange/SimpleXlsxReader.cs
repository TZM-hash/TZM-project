using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed record SimpleXlsxSheet(string Name, IReadOnlyList<IReadOnlyList<object?>> Rows, bool IsVeryHidden = false);

public static class SimpleXlsxReader
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<SimpleXlsxSheet> Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels")
            .Root!
            .Elements(PackageRelationshipsNamespace + "Relationship")
            .ToDictionary(item => (string)item.Attribute("Id")!, item => (string)item.Attribute("Target")!, StringComparer.Ordinal);
        var sharedStrings = ReadSharedStrings(archive);
        var result = new List<SimpleXlsxSheet>();
        foreach (var sheet in workbook.Root!.Element(SpreadsheetNamespace + "sheets")!.Elements(SpreadsheetNamespace + "sheet"))
        {
            var name = (string)sheet.Attribute("name")!;
            var relationshipId = (string)sheet.Attribute(OfficeRelationshipsNamespace + "id")!;
            var target = relationships[relationshipId].Replace('\\', '/');
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
            result.Add(new SimpleXlsxSheet(name, ReadRows(LoadXml(archive, path), sharedStrings), string.Equals((string?)sheet.Attribute("state"), "veryHidden", StringComparison.OrdinalIgnoreCase)));
        }

        return result;
    }

    private static List<IReadOnlyList<object?>> ReadRows(XDocument worksheet, IReadOnlyList<string> sharedStrings)
    {
        var result = new List<IReadOnlyList<object?>>();
        foreach (var row in worksheet.Descendants(SpreadsheetNamespace + "row"))
        {
            var values = new List<object?>();
            foreach (var cell in row.Elements(SpreadsheetNamespace + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? string.Empty;
                var columnIndex = ColumnIndex(reference);
                while (values.Count < columnIndex)
                {
                    values.Add(null);
                }

                values.Add(ReadCell(cell, sharedStrings));
            }

            result.Add(values);
        }

        return result;
    }

    private static object? ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
        {
            return ParseText(string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(item => item.Value)));
        }

        var value = cell.Element(SpreadsheetNamespace + "v")?.Value;
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return type switch
        {
            "b" => value == "1",
            "s" => ParseText(sharedStrings[int.Parse(value, CultureInfo.InvariantCulture)]),
            "str" => ParseText(value),
            _ => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)
        };
    }

    private static object ParseText(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : value;

    private static string[] ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Root!.Elements(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidDataException($"XLSX 缺少文件：{path}");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static int ColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var character in cellReference.TakeWhile(char.IsLetter))
        {
            index = index * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }

        return Math.Max(index - 1, 0);
    }
}
