using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Infrastructure.DataExchange;

/// <summary>
/// Builds the common workbook envelope used by data that can make a
/// round-trip through Excel.  Business columns stay in the caller's order;
/// technical columns are appended, hidden and locked by the worksheet.
/// </summary>
public sealed class RoundTripWorkbookBuilder
{
    public static IReadOnlyList<string> ControlColumnKeys { get; } =
    [
        "_record_id",
        "_business_key",
        "_row_version",
        "_dataset_key",
        "_dataset_version",
        "_export_batch_id"
    ];

    private static readonly Dictionary<string, string> ControlDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["_record_id"] = "系统记录 ID，不允许修改；新增行留空。",
            ["_business_key"] = "稳定业务编号，不允许修改；新增行需填写业务编号。",
            ["_row_version"] = "导出时的并发版本，不允许修改。",
            ["_dataset_key"] = "目标数据集，不允许修改。",
            ["_dataset_version"] = "数据集契约版本，不允许修改。",
            ["_export_batch_id"] = "来源导出批次，不允许修改。"
        };

    private readonly Func<DateTimeOffset> clock;

    public RoundTripWorkbookBuilder(Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public byte[] Build(RoundTripWorkbookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ExportBatchId))
        {
            throw new ArgumentException("导出批次不能为空。", nameof(request));
        }

        if (request.Sheets is null || request.Sheets.Count == 0)
        {
            throw new ArgumentException("至少需要一个业务工作表。", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DatasetVersion))
        {
            throw new ArgumentException("数据集版本不能为空。", nameof(request));
        }

        var workbook = new SimpleXlsxWorkbook();
        _ = request.ExportedAt ?? clock();
        var directoryRows = request.Sheets.Select(sheet => (IReadOnlyList<object?>)[
            DataExchangeValueLabels.Dataset(sheet.Dataset),
            new XlsxHyperlink(sheet.WorksheetName, $"'{sheet.WorksheetName}'!A1"),
            sheet.Rows.Count,
            request.SourcePage,
            request.ExportBatchId
        ]).ToArray();
        workbook.AddWorksheet("目录", ["数据集", "工作表", "记录数", "来源页面", "导出批次"], directoryRows,
            new XlsxWorksheetOptions([]));

        var descriptionRows = BuildDescriptionRows(request.Sheets);
        workbook.AddWorksheet("数据说明", ["工作表", "字段", "说明", "类型", "必填", "可回导", "计算字段", "控制列"], descriptionRows,
            new XlsxWorksheetOptions([]));

        foreach (var sheet in request.Sheets)
        {
            AddBusinessWorksheet(workbook, sheet, request);
        }

        return workbook.ToArray();
    }

    private static void AddBusinessWorksheet(
        SimpleXlsxWorkbook workbook,
        RoundTripWorkbookSheet sheet,
        RoundTripWorkbookRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet.Fields);
        ArgumentNullException.ThrowIfNull(sheet.Rows);

        var fields = sheet.Fields
            .Where(field => field.CanExport && !IsTechnicalKey(field.Key))
            .ToArray();
        if (fields.Length == 0)
        {
            throw new ArgumentException($"工作表“{sheet.WorksheetName}”没有可导出的业务字段。", nameof(sheet));
        }

        var headers = fields.Select(field => field.Label).Concat(ControlColumnKeys).ToArray();
        var rows = sheet.Rows.Select(row =>
        {
            var values = fields.Select(field => row.Values.GetValueOrDefault(field.Key)).ToList();
            values.Add(GetControlValue(row, "_record_id"));
            values.Add(GetControlValue(row, "_business_key"));
            values.Add(GetControlValue(row, "_row_version"));
            values.Add(sheet.Dataset.ToString());
            values.Add(request.DatasetVersion);
            values.Add(request.ExportBatchId);
            return (IReadOnlyList<object?>)values.ToArray();
        }).ToArray();

        var hiddenIndexes = Enumerable.Range(headers.Length - ControlColumnKeys.Count, ControlColumnKeys.Count).ToArray();
        workbook.AddWorksheet(sheet.WorksheetName, headers, rows,
            new XlsxWorksheetOptions(hiddenIndexes));
    }

    private static IEnumerable<IReadOnlyList<object?>> BuildDescriptionRows(IReadOnlyList<RoundTripWorkbookSheet> sheets)
    {
        foreach (var sheet in sheets)
        {
            foreach (var field in sheet.Fields.Where(field => field.CanExport && !IsTechnicalKey(field.Key)))
            {
                yield return [
                    sheet.WorksheetName,
                    field.Key,
                    field.Label,
                    field.DataType.ToString(),
                    field.IsRequired ? "是" : "否",
                    field.CanImport ? "是" : "否",
                    field.IsCalculated ? "是" : "否",
                    "否"
                ];
            }

            foreach (var control in ControlColumnKeys)
            {
                yield return [
                    sheet.WorksheetName,
                    control,
                    ControlDescriptions[control],
                    "控制列",
                    "否",
                    "否",
                    "否",
                    "是"
                ];
            }
        }
    }

    private static object? GetControlValue(RoundTripWorkbookRow row, string key) => key switch
    {
        "_record_id" => row.RecordId ?? row.Values.GetValueOrDefault("_record_id") ?? row.Values.GetValueOrDefault("_system_id"),
        "_business_key" => row.BusinessKey ?? row.Values.GetValueOrDefault("_business_key"),
        "_row_version" => row.RowVersion ?? row.Values.GetValueOrDefault("_row_version") ?? row.Values.GetValueOrDefault("_concurrency_stamp"),
        _ => row.Values.GetValueOrDefault(key)
    };

    private static bool IsTechnicalKey(string key) =>
        !string.IsNullOrWhiteSpace(key) && key[0] == '_';
}
