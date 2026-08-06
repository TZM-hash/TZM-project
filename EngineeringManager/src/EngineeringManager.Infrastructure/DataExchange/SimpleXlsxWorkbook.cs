using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed record XlsxHyperlink(string Text, string Target, bool External = false);

public sealed record XlsxFormula(string Formula, object? CachedValue = null);

public sealed record XlsxPageSetupOptions(
    int? PaperSize = null,
    bool Landscape = false,
    int? HorizontalDpi = null,
    int? FitToWidth = null,
    int? FitToHeight = null,
    bool FitToPage = false);

public sealed record XlsxPageMarginsOptions(
    double? Left = null,
    double? Right = null,
    double? Top = null,
    double? Bottom = null,
    double? Header = null,
    double? Footer = null);

public enum XlsxHorizontalAlignment
{
    General,
    Left,
    Center,
    Right
}

public sealed record XlsxColumnOptions(
    double? Width = null,
    bool WrapText = false,
    XlsxHorizontalAlignment HorizontalAlignment = XlsxHorizontalAlignment.General,
    string? NumberFormat = null,
    string? FontColor = null,
    string? FontName = null,
    double? FontSize = null,
    bool Bold = false,
    string? TotalFontName = null,
    double? TotalFontSize = null,
    bool TotalBold = false,
    bool? TotalWrapText = null,
    XlsxHorizontalAlignment? TotalHorizontalAlignment = null);

public sealed record XlsxWorksheetOptions(
    IReadOnlyCollection<int>? HiddenColumnIndexes = null,
    bool ProtectSheet = false,
    bool HiddenSheet = false,
    IReadOnlyDictionary<int, XlsxColumnOptions>? ColumnOptions = null,
    bool FreezeTopRow = false,
    bool AutoFilter = false,
    bool AutoFitWrappedRows = false,
    bool HideGridLines = false,
    IReadOnlyList<object?>? TotalRow = null,
    double? HeaderRowHeight = null,
    double? BodyRowHeight = null,
    double? TotalRowHeight = null,
    double? DefaultColumnWidth = null,
    double? DefaultRowHeight = null,
    int? ZoomScale = null,
    XlsxPageSetupOptions? PageSetup = null,
    XlsxPageMarginsOptions? PageMargins = null,
    bool RepeatHeaderRowOnPrint = false,
    string? HeaderBorderColor = null,
    string? BodyBorderColor = null,
    string? TotalBorderColor = null);

public sealed class SimpleXlsxWorkbook
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private readonly List<WorksheetData> worksheets = [];

    public void AddWorksheet(string name, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows) =>
        AddWorksheet(name, headers, rows, null);

    public void AddWorksheet(
        string name,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows,
        XlsxWorksheetOptions? options)
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

        var worksheetOptions = options ?? new XlsxWorksheetOptions();
        if (worksheetOptions.TotalRow is { Count: > 0 } totalRow && totalRow.Count > headers.Count)
        {
            throw new ArgumentException("合计行列数不能超过表头列数。", nameof(options));
        }

        worksheets.Add(new WorksheetData(name, headers.ToArray(), materializedRows, worksheetOptions));
    }

    public byte[] ToArray()
    {
        if (worksheets.Count == 0)
        {
            throw new InvalidOperationException("工作簿至少需要一个工作表。");
        }

        var styles = new StyleCatalog(worksheets);
        var includeStyles = styles.RequiresStyles;
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            WriteXml(archive, "[Content_Types].xml", CreateContentTypes(includeStyles));
            WriteXml(archive, "_rels/.rels", CreateRootRelationships());
            WriteXml(archive, "xl/workbook.xml", CreateWorkbook());
            WriteXml(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationships(includeStyles));
            if (includeStyles)
            {
                WriteXml(archive, "xl/styles.xml", styles.ToDocument());
            }
            for (var index = 0; index < worksheets.Count; index++)
            {
                WriteXml(archive, $"xl/worksheets/sheet{index + 1}.xml", CreateWorksheet(worksheets[index], styles, includeStyles));
                var externalLinks = GetExternalLinks(worksheets[index]);
                if (externalLinks.Length > 0)
                {
                    WriteXml(archive, $"xl/worksheets/_rels/sheet{index + 1}.xml.rels", CreateWorksheetRelationships(externalLinks));
                }
            }
        }

        return output.ToArray();
    }

    private XDocument CreateContentTypes(bool includeStyles)
    {
        XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        return new XDocument(
            new XElement(contentTypes + "Types",
                new XElement(contentTypes + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(contentTypes + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(contentTypes + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                includeStyles
                    ? new XElement(contentTypes + "Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))
                    : null,
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
        var printTitles = worksheets
            .Select((sheet, index) => sheet.Options.RepeatHeaderRowOnPrint
                ? new XElement(SpreadsheetNamespace + "definedName",
                    new XAttribute("name", "_xlnm.Print_Titles"),
                    new XAttribute("localSheetId", index),
                    $"{QuoteSheetName(sheet.Name)}!$1:$1")
                : null)
            .Where(item => item is not null)
            .ToArray();
        return new XDocument(new XElement(SpreadsheetNamespace + "workbook",
            new XAttribute(XNamespace.Xmlns + "r", relationships),
            new XElement(SpreadsheetNamespace + "sheets",
                worksheets.Select((sheet, index) => new XElement(SpreadsheetNamespace + "sheet",
                    new XAttribute("name", sheet.Name),
                    new XAttribute("sheetId", index + 1),
                    sheet.Options.HiddenSheet ? new XAttribute("state", "veryHidden") : null,
                    new XAttribute(relationships + "id", $"rId{index + 1}")))),
            printTitles.Length == 0 ? null : new XElement(SpreadsheetNamespace + "definedNames", printTitles)));
    }

    private XDocument CreateWorkbookRelationships(bool includeStyles)
    {
        XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(new XElement(relationships + "Relationships",
            worksheets.Select((_, index) => new XElement(relationships + "Relationship",
                    new XAttribute("Id", $"rId{index + 1}"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", $"worksheets/sheet{index + 1}.xml")))
                .Append(includeStyles
                    ? new XElement(relationships + "Relationship",
                        new XAttribute("Id", $"rId{worksheets.Count + 1}"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                        new XAttribute("Target", "styles.xml"))
                    : null)));
    }

    private static XDocument CreateWorksheet(WorksheetData worksheet, StyleCatalog styles, bool includeStyles)
    {
        var rows = new List<XElement> { CreateRow(1, worksheet.Headers.Cast<object?>().ToArray(), worksheet.Options, styles, includeStyles, isHeader: true) };
        rows.AddRange(worksheet.Rows.Select((row, index) => CreateRow(index + 2, row, worksheet.Options, styles, includeStyles, isHeader: false)));
        if (worksheet.Options.TotalRow is not null)
        {
            rows.Add(CreateRow(worksheet.Rows.Count + 2, worksheet.Options.TotalRow, worksheet.Options, styles, includeStyles, isHeader: false, isTotal: true));
        }
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var externalIndex = 0;
        var links = worksheet.Rows.SelectMany((row, rowIndex) => row.Select((value, columnIndex) => new { value, rowIndex, columnIndex }))
            .Where(item => item.value is XlsxHyperlink)
            .Select((item, index) =>
            {
                var link = (XlsxHyperlink)item.value!;
                var element = new XElement(SpreadsheetNamespace + "hyperlink", new XAttribute("ref", ColumnName(item.columnIndex + 1) + (item.rowIndex + 2)), new XAttribute("display", link.Text));
                if (link.External) element.Add(new XAttribute(relationships + "id", $"rId{++externalIndex}"));
                else element.Add(new XAttribute("location", link.Target.TrimStart('#')));
                return element;
            }).ToArray();
        var columns = CreateColumns(worksheet);
        var protection = worksheet.Options.ProtectSheet ? new XElement(SpreadsheetNamespace + "sheetProtection", new XAttribute("sheet", 1), new XAttribute("objects", 1), new XAttribute("scenarios", 1)) : null;
        var sheetProperties = worksheet.Options.PageSetup?.FitToPage == true
            ? new XElement(SpreadsheetNamespace + "sheetPr",
                new XElement(SpreadsheetNamespace + "pageSetUpPr", new XAttribute("fitToPage", 1)))
            : null;
        var sheetViews = CreateSheetViews(worksheet.Options);
        var sheetFormat = worksheet.Options.DefaultColumnWidth.HasValue || worksheet.Options.DefaultRowHeight.HasValue
            ? new XElement(SpreadsheetNamespace + "sheetFormatPr",
                worksheet.Options.DefaultColumnWidth.HasValue ? new XAttribute("defaultColWidth", FormatNumber(worksheet.Options.DefaultColumnWidth.Value)) : null,
                worksheet.Options.DefaultRowHeight.HasValue ? new XAttribute("defaultRowHeight", FormatNumber(worksheet.Options.DefaultRowHeight.Value)) : null)
            : null;
        var autoFilter = worksheet.Options.AutoFilter
            ? new XElement(SpreadsheetNamespace + "autoFilter", new XAttribute("ref", $"A1:{ColumnName(worksheet.Headers.Count)}{worksheet.Rows.Count + 1 + (worksheet.Options.TotalRow is null ? 0 : 1)}"))
            : null;
        var pageMargins = worksheet.Options.PageMargins is null ? null : CreatePageMargins(worksheet.Options.PageMargins);
        var pageSetup = worksheet.Options.PageSetup is null ? null : CreatePageSetup(worksheet.Options.PageSetup);
        return new XDocument(new XElement(SpreadsheetNamespace + "worksheet",
            new XAttribute(XNamespace.Xmlns + "r", relationships),
            sheetProperties,
            sheetViews,
            sheetFormat,
            columns,
            new XElement(SpreadsheetNamespace + "sheetData", rows),
            protection,
            autoFilter,
            links.Length == 0 ? null : new XElement(SpreadsheetNamespace + "hyperlinks", links),
            pageMargins,
            pageSetup));
    }

    private static XElement? CreateColumns(WorksheetData worksheet)
    {
        var hiddenIndexes = worksheet.Options.HiddenColumnIndexes?.ToHashSet() ?? [];
        var configuredColumns = worksheet.Options.ColumnOptions ?? new Dictionary<int, XlsxColumnOptions>();
        var columns = Enumerable.Range(0, worksheet.Headers.Count)
            .Where(index => hiddenIndexes.Contains(index) || configuredColumns.ContainsKey(index))
            .Select(index =>
            {
                configuredColumns.TryGetValue(index, out var options);
                var width = options?.Width ?? 12d;
                return new XElement(SpreadsheetNamespace + "col",
                    new XAttribute("min", index + 1),
                    new XAttribute("max", index + 1),
                    hiddenIndexes.Contains(index) ? new XAttribute("hidden", 1) : null,
                    new XAttribute("width", FormatNumber(width)),
                    new XAttribute("customWidth", 1));
            })
            .ToArray();
        return columns.Length == 0 ? null : new XElement(SpreadsheetNamespace + "cols", columns);
    }

    private static XElement? CreateSheetViews(XlsxWorksheetOptions options)
    {
        if (!options.FreezeTopRow && !options.HideGridLines && !options.ZoomScale.HasValue)
        {
            return null;
        }

        var sheetView = new XElement(SpreadsheetNamespace + "sheetView",
            new XAttribute("workbookViewId", 0),
            options.HideGridLines ? new XAttribute("showGridLines", 0) : null,
            options.ZoomScale.HasValue ? new XAttribute("zoomScale", options.ZoomScale.Value) : null);
        if (options.FreezeTopRow)
        {
            sheetView.Add(
                new XElement(SpreadsheetNamespace + "pane",
                    new XAttribute("ySplit", 1),
                    new XAttribute("topLeftCell", "A2"),
                    new XAttribute("activePane", "bottomLeft"),
                    new XAttribute("state", "frozen")),
                new XElement(SpreadsheetNamespace + "selection",
                    new XAttribute("pane", "bottomLeft"),
                    new XAttribute("activeCell", "A2"),
                    new XAttribute("sqref", "A2")));
        }

        return new XElement(SpreadsheetNamespace + "sheetViews", sheetView);
    }

    private static XlsxHyperlink[] GetExternalLinks(WorksheetData worksheet) => worksheet.Rows
        .SelectMany(row => row.OfType<XlsxHyperlink>())
        .Where(link => link.External)
        .ToArray();

    private static XDocument CreateWorksheetRelationships(IReadOnlyList<XlsxHyperlink> links)
    {
        XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(new XElement(relationships + "Relationships", links.Select((link, index) =>
            new XElement(relationships + "Relationship",
                new XAttribute("Id", $"rId{index + 1}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"),
                new XAttribute("Target", link.Target),
                new XAttribute("TargetMode", "External")))));
    }

    private static XElement CreateRow(
        int rowNumber,
        IReadOnlyList<object?> values,
        XlsxWorksheetOptions options,
        StyleCatalog styles,
        bool includeStyles,
        bool isHeader)
    {
        return CreateRow(rowNumber, values, options, styles, includeStyles, isHeader, isTotal: false);
    }

    private static XElement CreateRow(
        int rowNumber,
        IReadOnlyList<object?> values,
        XlsxWorksheetOptions options,
        StyleCatalog styles,
        bool includeStyles,
        bool isHeader,
        bool isTotal)
    {
        var height = EstimateRowHeight(values, options, isHeader, isTotal);
        var cells = values.Select((value, columnIndex) =>
        {
            var hidden = options.HiddenColumnIndexes?.Contains(columnIndex) ?? false;
            var editable = !isHeader && !hidden;
            var locked = options.ProtectSheet ? !editable : true;
            var hasVisualStyle = options.ColumnOptions is { Count: > 0 } || isTotal;
            var styleIndex = includeStyles && (hasVisualStyle || (!isHeader && !hidden))
                ? (int?)styles.GetCellStyleIndex(options, columnIndex, isHeader, isTotal, locked)
                : null;
            return CreateCell(ColumnName(columnIndex + 1) + rowNumber, value, styleIndex);
        });
        return new XElement(SpreadsheetNamespace + "row",
            new XAttribute("r", rowNumber),
            height is null ? null : new XAttribute("ht", height.Value.ToString("0.##", CultureInfo.InvariantCulture)),
            height is null ? null : new XAttribute("customHeight", 1),
            cells);
    }

    private static double? EstimateRowHeight(IReadOnlyList<object?> values, XlsxWorksheetOptions options, bool isHeader, bool isTotal)
    {
        if (isHeader && options.HeaderRowHeight.HasValue)
        {
            return options.HeaderRowHeight.Value;
        }

        if (isTotal && options.TotalRowHeight.HasValue)
        {
            return options.TotalRowHeight.Value;
        }

        if (!isHeader && !isTotal && options.BodyRowHeight.HasValue)
        {
            return options.BodyRowHeight.Value;
        }

        if (!options.AutoFitWrappedRows)
        {
            return null;
        }

        var maxLines = 1;
        for (var columnIndex = 0; columnIndex < values.Count; columnIndex++)
        {
            XlsxColumnOptions? columnOptions = null;
            if (options.ColumnOptions is not null)
            {
                options.ColumnOptions.TryGetValue(columnIndex, out columnOptions);
            }
            if (!isHeader && !(columnOptions?.WrapText ?? false))
            {
                continue;
            }

            var width = columnOptions?.Width ?? 12d;
            maxLines = Math.Max(maxLines, EstimateLineCount(CellText(values[columnIndex]), width));
        }

        return Math.Clamp(maxLines * 15d, 30d, 90d);
    }

    private static int EstimateLineCount(string text, double width)
    {
        var charactersPerLine = Math.Max(8, (int)Math.Floor(width * 0.9d));
        return text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / (double)charactersPerLine)));
    }

    private static XElement CreateCell(string reference, object? value, int? styleIndex)
    {
        if (value is null)
        {
            return new XElement(SpreadsheetNamespace + "c", new XAttribute("r", reference), styleIndex is null ? null : new XAttribute("s", styleIndex));
        }

        if (value is bool boolean)
        {
            return new XElement(SpreadsheetNamespace + "c", new XAttribute("r", reference), styleIndex is null ? null : new XAttribute("s", styleIndex), new XAttribute("t", "b"), new XElement(SpreadsheetNamespace + "v", boolean ? "1" : "0"));
        }

        if (value is XlsxFormula formula)
        {
            return new XElement(SpreadsheetNamespace + "c",
                new XAttribute("r", reference),
                styleIndex is null ? null : new XAttribute("s", styleIndex),
                new XElement(SpreadsheetNamespace + "f", formula.Formula),
                formula.CachedValue is null ? null : new XElement(SpreadsheetNamespace + "v", Convert.ToString(formula.CachedValue, CultureInfo.InvariantCulture)));
        }

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            return new XElement(SpreadsheetNamespace + "c", new XAttribute("r", reference), styleIndex is null ? null : new XAttribute("s", styleIndex), new XElement(SpreadsheetNamespace + "v", Convert.ToString(value, CultureInfo.InvariantCulture)));
        }

        var text = CellText(value);
        return new XElement(SpreadsheetNamespace + "c",
            new XAttribute("r", reference),
            styleIndex is null ? null : new XAttribute("s", styleIndex),
            new XAttribute("t", "inlineStr"),
            new XElement(SpreadsheetNamespace + "is", new XElement(SpreadsheetNamespace + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
    }

    private static string CellText(object? value) => value switch
    {
        XlsxHyperlink link => link.Text,
        XlsxFormula formula => CellText(formula.CachedValue),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

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

    private static string FormatNumber(double value) => value.ToString("0.#################", CultureInfo.InvariantCulture);

    private static string QuoteSheetName(string name) => $"'{name.Replace("'", "''", StringComparison.Ordinal)}'";

    private static XElement CreatePageMargins(XlsxPageMarginsOptions options) =>
        new(SpreadsheetNamespace + "pageMargins",
            options.Left.HasValue ? new XAttribute("left", FormatNumber(options.Left.Value)) : null,
            options.Right.HasValue ? new XAttribute("right", FormatNumber(options.Right.Value)) : null,
            options.Top.HasValue ? new XAttribute("top", FormatNumber(options.Top.Value)) : null,
            options.Bottom.HasValue ? new XAttribute("bottom", FormatNumber(options.Bottom.Value)) : null,
            options.Header.HasValue ? new XAttribute("header", FormatNumber(options.Header.Value)) : null,
            options.Footer.HasValue ? new XAttribute("footer", FormatNumber(options.Footer.Value)) : null);

    private static XElement CreatePageSetup(XlsxPageSetupOptions options) =>
        new(SpreadsheetNamespace + "pageSetup",
            options.PaperSize.HasValue ? new XAttribute("paperSize", options.PaperSize.Value) : null,
            options.Landscape ? new XAttribute("orientation", "landscape") : null,
            options.HorizontalDpi.HasValue ? new XAttribute("horizontalDpi", options.HorizontalDpi.Value) : null,
            options.FitToWidth.HasValue ? new XAttribute("fitToWidth", options.FitToWidth.Value) : null,
            options.FitToHeight.HasValue ? new XAttribute("fitToHeight", options.FitToHeight.Value) : null);

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

    private sealed record WorksheetData(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows, XlsxWorksheetOptions Options);

    private sealed record FontDefinition(string Name, double Size, bool Bold, string Color);

    private sealed record StyleDefinition(
        bool Locked,
        bool Header,
        bool Total,
        bool WrapText,
        XlsxHorizontalAlignment HorizontalAlignment,
        string? NumberFormat,
        string? FontName,
        double? FontSize,
        bool Bold,
        string? FontColor,
        string? BorderColor);

    private sealed class StyleCatalog
    {
        private readonly List<StyleDefinition> definitions =
        [
            new(true, false, false, false, XlsxHorizontalAlignment.General, null, null, null, false, null, null),
            new(false, false, false, false, XlsxHorizontalAlignment.General, null, null, null, false, null, null)
        ];
        private readonly Dictionary<StyleDefinition, int> indexes = new();

        public StyleCatalog(IReadOnlyList<WorksheetData> worksheets)
        {
            RequiresStyles = worksheets.Any(worksheet => worksheet.Options.ProtectSheet
                || worksheet.Options.ColumnOptions is { Count: > 0 }
                || worksheet.Options.TotalRow is not null);
            if (!RequiresStyles)
            {
                return;
            }

            foreach (var worksheet in worksheets.Where(worksheet => worksheet.Options.ColumnOptions is { Count: > 0 } || worksheet.Options.TotalRow is not null))
            {
                for (var columnIndex = 0; columnIndex < worksheet.Headers.Count; columnIndex++)
                {
                    _ = GetCellStyleIndex(worksheet.Options, columnIndex, isHeader: true, isTotal: false, locked: true);
                    var hidden = worksheet.Options.HiddenColumnIndexes?.Contains(columnIndex) ?? false;
                    var locked = worksheet.Options.ProtectSheet ? hidden : true;
                    _ = GetCellStyleIndex(worksheet.Options, columnIndex, isHeader: false, isTotal: false, locked);
                    if (worksheet.Options.TotalRow is not null)
                    {
                        _ = GetCellStyleIndex(worksheet.Options, columnIndex, isHeader: false, isTotal: true, locked);
                    }
                }
            }
        }

        public bool RequiresStyles { get; }

        public int GetCellStyleIndex(XlsxWorksheetOptions options, int columnIndex, bool isHeader, bool isTotal, bool locked)
        {
            if (options.ColumnOptions is not { Count: > 0 } && !isTotal)
            {
                return locked ? 0 : 1;
            }

            XlsxColumnOptions? columnOptions = null;
            options.ColumnOptions?.TryGetValue(columnIndex, out columnOptions);
            columnOptions ??= new XlsxColumnOptions();
            var definition = isHeader
                ? new StyleDefinition(
                    locked,
                    Header: true,
                    Total: false,
                    WrapText: true,
                    HorizontalAlignment: XlsxHorizontalAlignment.Center,
                    NumberFormat: columnOptions.NumberFormat,
                    FontName: "宋体",
                    FontSize: 10,
                    Bold: true,
                    FontColor: "FF1F4E78",
                    BorderColor: options.HeaderBorderColor ?? "FF8091A3")
                : isTotal
                    ? new StyleDefinition(
                        locked,
                        Header: false,
                        Total: true,
                        WrapText: columnOptions.TotalWrapText ?? columnOptions.WrapText,
                        HorizontalAlignment: columnOptions.TotalHorizontalAlignment ?? columnOptions.HorizontalAlignment,
                        NumberFormat: columnOptions.NumberFormat,
                        FontName: columnOptions.TotalFontName ?? "Calibri",
                        FontSize: columnOptions.TotalFontSize ?? 10,
                        Bold: columnOptions.TotalBold,
                        FontColor: "FFFF0000",
                        BorderColor: options.TotalBorderColor ?? options.BodyBorderColor ?? "FFD9E2EC")
                    : new StyleDefinition(
                        locked,
                        Header: false,
                        Total: false,
                        WrapText: columnOptions.WrapText,
                        HorizontalAlignment: columnOptions.HorizontalAlignment,
                        NumberFormat: columnOptions.NumberFormat,
                        FontName: columnOptions.FontName ?? "Calibri",
                        FontSize: columnOptions.FontSize ?? 11,
                        Bold: columnOptions.Bold,
                        FontColor: columnOptions.FontColor,
                        BorderColor: options.BodyBorderColor ?? "FFD9E2EC");
            if (indexes.TryGetValue(definition, out var existingIndex))
            {
                return existingIndex;
            }

            var index = definitions.Count;
            definitions.Add(definition);
            indexes[definition] = index;
            return index;
        }

        public XDocument ToDocument()
        {
            var customFormats = definitions
                .Select(item => item.NumberFormat)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Where(item => !string.Equals(item, "0%", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Select((format, index) => new { Format = format!, Id = 164 + index })
                .ToDictionary(item => item.Format, item => item.Id, StringComparer.Ordinal);
            var fonts = definitions
                .Select(FontFor)
                .Distinct()
                .ToArray();
            var fontIndexes = fonts
                .Select((font, index) => new { font, index })
                .ToDictionary(item => item.font, item => item.index);
            var borderColors = definitions
                .Select(item => NormalizeColor(item.BorderColor))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var borderIndexes = borderColors
                .Select((color, index) => new { color, index = index + 1 })
                .ToDictionary(item => item.color, item => item.index, StringComparer.Ordinal);

            return new XDocument(new XElement(SpreadsheetNamespace + "styleSheet",
                new XElement(SpreadsheetNamespace + "numFmts",
                    new XAttribute("count", customFormats.Count),
                    customFormats.Select(item => new XElement(SpreadsheetNamespace + "numFmt",
                        new XAttribute("numFmtId", item.Value),
                        new XAttribute("formatCode", item.Key)))),
                new XElement(SpreadsheetNamespace + "fonts",
                    new XAttribute("count", fonts.Length),
                    fonts.Select(CreateFont)),
                new XElement(SpreadsheetNamespace + "fills",
                    new XAttribute("count", 3),
                    new XElement(SpreadsheetNamespace + "fill", new XElement(SpreadsheetNamespace + "patternFill", new XAttribute("patternType", "none"))),
                    new XElement(SpreadsheetNamespace + "fill", new XElement(SpreadsheetNamespace + "patternFill", new XAttribute("patternType", "gray125"))),
                    new XElement(SpreadsheetNamespace + "fill", new XElement(SpreadsheetNamespace + "patternFill", new XAttribute("patternType", "solid"), new XElement(SpreadsheetNamespace + "fgColor", new XAttribute("rgb", "FFD9EAF7")), new XElement(SpreadsheetNamespace + "bgColor", new XAttribute("indexed", 64))))),
                new XElement(SpreadsheetNamespace + "borders",
                    new XAttribute("count", borderColors.Length + 1),
                    new XElement(SpreadsheetNamespace + "border"),
                    borderColors.Select(CreateBorder)),
                new XElement(SpreadsheetNamespace + "cellStyleXfs", new XAttribute("count", 1), new XElement(SpreadsheetNamespace + "xf", new XAttribute("xfId", 0))),
                new XElement(SpreadsheetNamespace + "cellXfs",
                    new XAttribute("count", definitions.Count),
                    definitions.Select((definition, index) => CreateCellFormat(definition, index, customFormats, fontIndexes, borderIndexes)))));
        }

        private static XElement CreateCellFormat(
            StyleDefinition definition,
            int index,
            Dictionary<string, int> customFormats,
            IReadOnlyDictionary<FontDefinition, int> fontIndexes,
            Dictionary<string, int> borderIndexes)
        {
            if (index < 2)
            {
                return new XElement(SpreadsheetNamespace + "xf",
                    new XAttribute("xfId", 0),
                    index == 1 ? new XAttribute("applyProtection", 1) : null,
                    new XElement(SpreadsheetNamespace + "protection", new XAttribute("locked", definition.Locked ? 1 : 0)));
            }

            var fontId = fontIndexes[FontFor(definition)];
            var alignment = new XElement(SpreadsheetNamespace + "alignment",
                new XAttribute("vertical", "center"),
                definition.WrapText ? new XAttribute("wrapText", 1) : null,
                definition.HorizontalAlignment == XlsxHorizontalAlignment.General ? null : new XAttribute("horizontal", AlignmentName(definition.HorizontalAlignment)));
            return new XElement(SpreadsheetNamespace + "xf",
                new XAttribute("xfId", 0),
                new XAttribute("fontId", fontId),
                new XAttribute("fillId", definition.Header ? 2 : 0),
                new XAttribute("borderId", borderIndexes[NormalizeColor(definition.BorderColor)]),
                new XAttribute("numFmtId", NumberFormatId(definition.NumberFormat, customFormats)),
                new XAttribute("applyFont", 1),
                new XAttribute("applyFill", 1),
                new XAttribute("applyBorder", 1),
                definition.NumberFormat is null ? null : new XAttribute("applyNumberFormat", 1),
                new XAttribute("applyAlignment", 1),
                new XAttribute("applyProtection", 1),
                alignment,
                new XElement(SpreadsheetNamespace + "protection", new XAttribute("locked", definition.Locked ? 1 : 0)));
        }

        private static FontDefinition FontFor(StyleDefinition definition) =>
            new(
                definition.FontName ?? "Calibri",
                definition.FontSize ?? 11,
                definition.Bold,
                NormalizeColor(definition.FontColor));

        private static int NumberFormatId(string? format, Dictionary<string, int> customFormats) =>
            format switch
            {
                "0%" => 9,
                null or "" => 0,
                _ when customFormats.TryGetValue(format, out var customId) => customId,
                _ => 0
            };

        private static XElement CreateFont(FontDefinition font) =>
            new(SpreadsheetNamespace + "font",
                font.Bold ? new XElement(SpreadsheetNamespace + "b") : null,
                new XElement(SpreadsheetNamespace + "sz", new XAttribute("val", FormatNumber(font.Size))),
                new XElement(SpreadsheetNamespace + "color", new XAttribute("rgb", font.Color)),
                new XElement(SpreadsheetNamespace + "name", new XAttribute("val", font.Name)),
                new XElement(SpreadsheetNamespace + "charset", new XAttribute("val", 134)));

        private static XElement CreateBorder(string color) =>
            new(SpreadsheetNamespace + "border",
                new XElement(SpreadsheetNamespace + "left", new XAttribute("style", "thin"), new XElement(SpreadsheetNamespace + "color", new XAttribute("rgb", color))),
                new XElement(SpreadsheetNamespace + "right", new XAttribute("style", "thin"), new XElement(SpreadsheetNamespace + "color", new XAttribute("rgb", color))),
                new XElement(SpreadsheetNamespace + "top", new XAttribute("style", "thin"), new XElement(SpreadsheetNamespace + "color", new XAttribute("rgb", color))),
                new XElement(SpreadsheetNamespace + "bottom", new XAttribute("style", "thin"), new XElement(SpreadsheetNamespace + "color", new XAttribute("rgb", color))),
                new XElement(SpreadsheetNamespace + "diagonal"));

        private static string AlignmentName(XlsxHorizontalAlignment alignment) => alignment switch
        {
            XlsxHorizontalAlignment.Left => "left",
            XlsxHorizontalAlignment.Center => "center",
            XlsxHorizontalAlignment.Right => "right",
            _ => "general"
        };

        private static string NormalizeColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "FF000000";
            }

            var color = value.Trim().TrimStart('#');
            return color.Length switch
            {
                6 => "FF" + color.ToUpperInvariant(),
                8 => color.ToUpperInvariant(),
                _ => "FF000000"
            };
        }
    }
}
