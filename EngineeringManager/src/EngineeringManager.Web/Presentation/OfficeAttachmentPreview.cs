using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EngineeringManager.Infrastructure.DataExchange;
using NPOI.HSSF.UserModel;
using NPOI.HWPF;
using NPOI.HWPF.Extractor;
using NPOI.POIFS.FileSystem;
using NPOI.SS.UserModel;

namespace EngineeringManager.Web.Presentation;

public static class OfficeAttachmentPreview
{
    private static readonly XNamespace WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace PresentationTextNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";

    private const int MaxParagraphs = 400;
    private const int MaxRows = 200;
    private const int MaxColumns = 50;
    private const int MaxSlides = 100;
    private const int MaxPptFragments = 200;

    public static string? Create(string fileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".docx" => CreateDocx(fileName, content),
            ".xlsx" => CreateXlsx(fileName, content),
            ".pptx" => CreatePptx(fileName, content),
            ".doc" => CreateDoc(fileName, content),
            ".xls" => CreateXls(fileName, content),
            ".ppt" => CreatePpt(fileName, content),
            _ => null
        };
    }

    private static string CreateDocx(string fileName, byte[] content)
    {
        try
        {
            using var archive = OpenArchive(content);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null) return ErrorPage(fileName, "此 DOCX 文件缺少文档内容，无法预览。");
            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            var body = document.Root?.Element(WordNamespace + "body");
            if (body is null) return ErrorPage(fileName, "此 DOCX 文件没有可预览的内容。");
            var blocks = body.Elements().Select(element => element.Name == WordNamespace + "tbl"
                ? RenderWordTable(element)
                : element.Name == WordNamespace + "p" ? RenderWordParagraph(element) : string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(MaxParagraphs);
            return Page(fileName, string.Join(Environment.NewLine, blocks));
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException)
        {
            return ErrorPage(fileName, "Office 文件内容损坏或格式不受支持，请下载后用 Office 打开。");
        }
    }

    private static string CreateXlsx(string fileName, byte[] content)
    {
        try
        {
            var sheets = SimpleXlsxReader.Read(content).Where(sheet => !sheet.IsVeryHidden).Take(12).ToArray();
            if (sheets.Length == 0) return ErrorPage(fileName, "此 XLSX 文件没有可预览的工作表。");
            var sections = sheets.Select(sheet =>
            {
                var rows = sheet.Rows.Take(MaxRows).Select(row =>
                    $"<tr>{string.Join(string.Empty, row.Take(MaxColumns).Select(value => $"<td>{Encode(FormatValue(value))}</td>"))}</tr>");
                return $"<section><h2>{Encode(sheet.Name)}</h2><div class=\"sheet-wrap\"><table><tbody>{string.Join(string.Empty, rows)}</tbody></table></div></section>";
            });
            return Page(fileName, string.Join(Environment.NewLine, sections));
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException or FormatException or KeyNotFoundException)
        {
            return ErrorPage(fileName, "Office 文件内容损坏或格式不受支持，请下载后用 Office 打开。");
        }
    }

    private static string CreatePptx(string fileName, byte[] content)
    {
        try
        {
            using var archive = OpenArchive(content);
            var slides = archive.Entries
                .Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => SlideNumber(entry.FullName)).Take(MaxSlides).ToArray();
            if (slides.Length == 0) return ErrorPage(fileName, "此 PPTX 文件没有可预览的幻灯片。");
            var sections = new List<string>(slides.Length);
            for (var index = 0; index < slides.Length; index++)
            {
                using var stream = slides[index].Open();
                var document = XDocument.Load(stream);
                var texts = document.Descendants(PresentationTextNamespace + "t")
                    .Select(node => node.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Take(80)
                    .Select(value => $"<p>{Encode(value)}</p>");
                sections.Add($"<section><h2>幻灯片 {index + 1}</h2>{string.Join(string.Empty, texts)}</section>");
            }

            return Page(fileName, string.Join(Environment.NewLine, sections));
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException)
        {
            return ErrorPage(fileName, "Office 文件内容损坏或格式不受支持，请下载后用 Office 打开。");
        }
    }

    private static string CreateDoc(string fileName, byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            var document = new HWPFDocument(stream);
            var extractor = new WordExtractor(document);
            var paragraphs = extractor.ParagraphText
                .Select(value => value?.TrimEnd('\r', '\n', '\a') ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(MaxParagraphs)
                .Select(value => $"<p>{Encode(value)}</p>")
                .ToArray();
            if (paragraphs.Length == 0)
            {
                var fallback = extractor.Text?.Trim();
                if (string.IsNullOrWhiteSpace(fallback)) return ErrorPage(fileName, "此 DOC 文件没有可预览的文本内容。");
                return Page(fileName, $"<p>{Encode(fallback)}</p>");
            }

            return Page(fileName, string.Join(Environment.NewLine, paragraphs));
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException or NullReferenceException)
        {
            return ErrorPage(fileName, "Office 文件内容损坏或格式不受支持，请下载后用 Office 打开。");
        }
    }

    private static string CreateXls(string fileName, byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            var workbook = new HSSFWorkbook(stream);
            var formatter = new DataFormatter(CultureInfo.InvariantCulture);
            if (workbook.NumberOfSheets == 0) return ErrorPage(fileName, "此 XLS 文件没有可预览的工作表。");

            var sections = new List<string>();
            for (var sheetIndex = 0; sheetIndex < workbook.NumberOfSheets && sheetIndex < 12; sheetIndex++)
            {
                if (workbook.IsSheetVeryHidden(sheetIndex)) continue;
                var sheet = workbook.GetSheetAt(sheetIndex);
                var rows = new List<string>();
                var lastRow = Math.Min(sheet.LastRowNum, MaxRows - 1);
                for (var rowIndex = 0; rowIndex <= lastRow; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (row is null)
                    {
                        rows.Add("<tr></tr>");
                        continue;
                    }

                    var lastCell = Math.Min((int)row.LastCellNum, MaxColumns);
                    var cells = new List<string>(Math.Max(lastCell, 0));
                    for (var cellIndex = 0; cellIndex < lastCell; cellIndex++)
                    {
                        var cell = row.GetCell(cellIndex);
                        cells.Add($"<td>{Encode(cell is null ? string.Empty : formatter.FormatCellValue(cell))}</td>");
                    }

                    rows.Add($"<tr>{string.Join(string.Empty, cells)}</tr>");
                }

                sections.Add($"<section><h2>{Encode(sheet.SheetName)}</h2><div class=\"sheet-wrap\"><table><tbody>{string.Join(string.Empty, rows)}</tbody></table></div></section>");
            }

            return sections.Count == 0
                ? ErrorPage(fileName, "此 XLS 文件没有可预览的工作表。")
                : Page(fileName, string.Join(Environment.NewLine, sections));
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException)
        {
            return ErrorPage(fileName, "Office 文件内容损坏或格式不受支持，请下载后用 Office 打开。");
        }
    }

    private static string CreatePpt(string fileName, byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            var fileSystem = new POIFSFileSystem(stream);
            var fragments = ExtractPptTextFragments(fileSystem)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Take(MaxPptFragments)
                .Select(value => $"<p>{Encode(value)}</p>")
                .ToArray();
            if (fragments.Length == 0) return ErrorPage(fileName, "此 PPT 文件没有可预览的文本内容。");
            return Page(fileName, string.Join(Environment.NewLine, fragments));
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException or NullReferenceException)
        {
            return ErrorPage(fileName, "Office 文件内容损坏或格式不受支持，请下载后用 Office 打开。");
        }
    }

    private static IEnumerable<string> ExtractPptTextFragments(POIFSFileSystem fileSystem)
    {
        foreach (var entryName in fileSystem.Root.EntryNames)
        {
            var entry = fileSystem.Root.GetEntry(entryName);
            if (!entry.IsDocumentEntry) continue;
            if (string.Equals(entryName, "Pictures", StringComparison.OrdinalIgnoreCase)) continue;

            var bytes = ReadDocumentBytes(fileSystem, entryName);
            if (bytes.Length == 0) continue;
            foreach (var fragment in ExtractTextFromPptRecords(bytes))
            {
                yield return fragment;
            }
        }
    }

    private static byte[] ReadDocumentBytes(POIFSFileSystem fileSystem, string entryName)
    {
        using var documentStream = fileSystem.CreateDocumentInputStream(entryName);
        var length = checked((int)documentStream.Length);
        if (length <= 0) return [];

        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = documentStream.Read(buffer, offset, length - offset);
            if (read <= 0) break;
            offset += read;
        }

        return offset == length ? buffer : buffer.AsSpan(0, offset).ToArray();
    }

    private static IEnumerable<string> ExtractTextFromPptRecords(byte[] data)
    {
        var offset = 0;
        while (offset + 8 <= data.Length)
        {
            var info = BitConverter.ToUInt16(data, offset);
            var type = BitConverter.ToUInt16(data, offset + 2);
            var length = BitConverter.ToInt32(data, offset + 4);
            if (length < 0 || offset + 8 + length > data.Length) yield break;

            var version = info & 0x000F;
            var payloadOffset = offset + 8;
            if (type is 0x0FA0 or 0x0FA8 or 0x0FBA)
            {
                var text = type == 0x0FA0 || type == 0x0FBA
                    ? Encoding.Unicode.GetString(data, payloadOffset, length)
                    : Encoding.GetEncoding(1252).GetString(data, payloadOffset, length);
                text = text.Replace('\0', ' ').Trim();
                if (!string.IsNullOrWhiteSpace(text)) yield return text;
            }
            else if (version == 0xF && length > 0)
            {
                // Container: recursively walk children within this record payload.
                var end = payloadOffset + length;
                var childOffset = payloadOffset;
                while (childOffset + 8 <= end)
                {
                    var childInfo = BitConverter.ToUInt16(data, childOffset);
                    var childType = BitConverter.ToUInt16(data, childOffset + 2);
                    var childLength = BitConverter.ToInt32(data, childOffset + 4);
                    if (childLength < 0 || childOffset + 8 + childLength > end) break;
                    if (childType is 0x0FA0 or 0x0FA8 or 0x0FBA)
                    {
                        var text = childType == 0x0FA0 || childType == 0x0FBA
                            ? Encoding.Unicode.GetString(data, childOffset + 8, childLength)
                            : Encoding.GetEncoding(1252).GetString(data, childOffset + 8, childLength);
                        text = text.Replace('\0', ' ').Trim();
                        if (!string.IsNullOrWhiteSpace(text)) yield return text;
                    }
                    else if ((childInfo & 0x000F) == 0xF && childLength > 0)
                    {
                        foreach (var nested in ExtractTextFromPptRecords(data.AsSpan(childOffset + 8, childLength).ToArray()))
                        {
                            yield return nested;
                        }
                    }

                    childOffset += 8 + childLength;
                    if ((childLength & 1) == 1) childOffset++; // Word alignment sometimes applies; tolerate both.
                }
            }

            offset += 8 + length;
        }
    }

    private static string RenderWordParagraph(XElement paragraph)
    {
        var text = string.Concat(paragraph.Descendants(WordNamespace + "t").Select(node => node.Value));
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $"<p>{Encode(text)}</p>";
    }

    private static string RenderWordTable(XElement table)
    {
        var rows = table.Elements(WordNamespace + "tr").Take(MaxRows).Select(row =>
        {
            var cells = row.Elements(WordNamespace + "tc").Take(MaxColumns).Select(cell =>
                $"<td>{Encode(string.Concat(cell.Descendants(WordNamespace + "t").Select(item => item.Value)))}</td>");
            return $"<tr>{string.Join(string.Empty, cells)}</tr>";
        });
        return $"<div class=\"sheet-wrap\"><table><tbody>{string.Join(string.Empty, rows)}</tbody></table></div>";
    }

    private static ZipArchive OpenArchive(byte[] content) => new(new MemoryStream(content, writable: false), ZipArchiveMode.Read);

    private static int SlideNumber(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(fileName["slide".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : int.MaxValue;
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Page(string fileName, string body) => $"<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{Encode(fileName)}</title><style>{Styles}</style></head><body><main><h1>{Encode(fileName)}</h1>{body}</main></body></html>";

    private static string ErrorPage(string fileName, string message) => Page(fileName, $"<p class=\"notice\">{Encode(message)}</p>");

    private const string Styles = "body{margin:0;padding:24px;color:#172033;background:#f5f8fc;font:14px/1.6 system-ui,-apple-system,\\\"Segoe UI\\\",sans-serif}main{max-width:1100px;margin:0 auto;padding:20px;background:#fff;border:1px solid #d9e1ed;border-radius:8px;box-shadow:0 8px 24px #17203314}h1{margin:0 0 18px;font-size:18px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}h2{margin:18px 0 8px;font-size:15px}p{margin:8px 0;white-space:pre-wrap}.sheet-wrap{overflow:auto;margin:8px 0 16px}table{border-collapse:collapse;min-width:100%}td{min-width:80px;padding:7px 9px;border:1px solid #d9e1ed;vertical-align:top}.notice{padding:14px;color:#7a4d00;background:#fff8df;border:1px solid #f1d18a;border-radius:6px}@media(max-width:680px){body{padding:8px}main{padding:14px}}";
}
