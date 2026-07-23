using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EngineeringManager.Infrastructure.DataExchange;

namespace EngineeringManager.Web.Presentation;

public static class OfficeAttachmentPreview
{
    private static readonly XNamespace WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace PresentationTextNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public static string? Create(string fileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".docx" => CreateDocx(fileName, content),
            ".xlsx" => CreateXlsx(fileName, content),
            ".pptx" => CreatePptx(fileName, content),
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
                .Where(value => !string.IsNullOrWhiteSpace(value));
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
                var rows = sheet.Rows.Take(200).Select(row =>
                    $"<tr>{string.Join(string.Empty, row.Take(50).Select(value => $"<td>{Encode(FormatValue(value))}</td>"))}</tr>");
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
                .OrderBy(entry => SlideNumber(entry.FullName)).Take(100).ToArray();
            if (slides.Length == 0) return ErrorPage(fileName, "此 PPTX 文件没有可预览的幻灯片。");
            var sections = new List<string>(slides.Length);
            for (var index = 0; index < slides.Length; index++)
            {
                using var stream = slides[index].Open();
                var document = XDocument.Load(stream);
                var text = string.Join(" ", document.Descendants(PresentationTextNamespace + "t").Select(item => item.Value).Where(value => !string.IsNullOrWhiteSpace(value)));
                sections.Add($"<section><h2>幻灯片 {index + 1}</h2><p>{Encode(text)}</p></section>");
            }
            return Page(fileName, string.Join(Environment.NewLine, sections));
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException)
        {
            return ErrorPage(fileName, "Office 文件内容损坏或格式不受支持，请下载后用 Office 打开。");
        }
    }

    private static string RenderWordParagraph(XElement paragraph)
    {
        var text = string.Concat(paragraph.Descendants(WordNamespace + "t").Select(item => item.Value));
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $"<p>{Encode(text)}</p>";
    }

    private static string RenderWordTable(XElement table)
    {
        var rows = table.Elements(WordNamespace + "tr").Take(100).Select(row =>
        {
            var cells = row.Elements(WordNamespace + "tc").Take(50).Select(cell =>
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
