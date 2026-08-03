using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using System.Globalization;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed record RoundTripWorkbookContext(
    SimpleXlsxSheet BusinessSheet,
    IReadOnlyList<ImportRowIdentity> RowIdentities,
    string DatasetVersion,
    string? ExportBatchId,
    IReadOnlyList<ImportErrorDto> Errors);

/// <summary>
/// Recognizes the standard workbook envelope without treating an arbitrary
/// Excel file as a system export.  Legacy workbooks continue through the
/// existing header-mapping path when the envelope is absent.
/// </summary>
public static class ImportRoundTripMetadataReader
{
    public static bool TryRead(
        IReadOnlyList<SimpleXlsxSheet> sheets,
        ExportDataset expectedDataset,
        out RoundTripWorkbookContext? context)
    {
        context = null;
        if (!sheets.Any(item => item.Name == "目录") || !sheets.Any(item => item.Name == "数据说明"))
        {
            return false;
        }

        var expectedKey = expectedDataset.ToString();
        var candidates = sheets
            .Where(item => item.Name is not ("目录" or "数据说明") && item.Rows.Count > 0)
            .Where(item => item.Rows[0].Select(ToHeader).Contains("_dataset_key", StringComparer.Ordinal))
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        var selected = candidates.FirstOrDefault(item => item.Rows.Skip(1).Any(row =>
        {
            var index = Array.IndexOf(item.Rows[0].Select(ToHeader).ToArray(), "_dataset_key");
            return index >= 0 && string.Equals(index < row.Count ? ToText(row[index]) : null, expectedKey, StringComparison.Ordinal);
        })) ?? candidates[0];
        var headers = selected.Rows[0].Select(ToHeader).ToArray();
        var indexes = headers.Select((header, index) => (header, index)).ToDictionary(item => item.header, item => item.index, StringComparer.Ordinal);
        var errors = new List<ImportErrorDto>();
        foreach (var required in RoundTripWorkbookBuilder.ControlColumnKeys)
        {
            if (!indexes.ContainsKey(required))
            {
                errors.Add(new ImportErrorDto(1, required, "标准往返工作簿缺少控制列。", null));
            }
        }

        var identities = new List<ImportRowIdentity>();
        string? datasetVersion = null;
        string? exportBatchId = null;
        for (var rowIndex = 1; rowIndex < selected.Rows.Count; rowIndex++)
        {
            var row = selected.Rows[rowIndex];
            var identity = new ImportRowIdentity(
                ReadValue(indexes, row, "_record_id"),
                ReadValue(indexes, row, "_business_key"),
                ReadValue(indexes, row, "_row_version"),
                ReadValue(indexes, row, "_dataset_key") ?? string.Empty,
                ReadValue(indexes, row, "_dataset_version") ?? string.Empty,
                ReadValue(indexes, row, "_export_batch_id") ?? string.Empty);
            identities.Add(identity);

            var excelRow = rowIndex + 1;
            if (!string.IsNullOrWhiteSpace(identity.RecordId) || !string.IsNullOrWhiteSpace(identity.BusinessKey))
            {
                if (!string.IsNullOrWhiteSpace(identity.DatasetKey) && !string.Equals(identity.DatasetKey, expectedKey, StringComparison.Ordinal))
                {
                    errors.Add(new ImportErrorDto(excelRow, "_dataset_key", "工作簿数据集与当前导入模块不一致。", identity.DatasetKey));
                }
                if (string.IsNullOrWhiteSpace(identity.DatasetVersion))
                {
                    errors.Add(new ImportErrorDto(excelRow, "_dataset_version", "已有记录必须保留数据集版本。", null));
                }
                if (string.IsNullOrWhiteSpace(identity.ExportBatchId))
                {
                    errors.Add(new ImportErrorDto(excelRow, "_export_batch_id", "已有记录必须保留来源导出批次。", null));
                }
            }

            if (!string.IsNullOrWhiteSpace(identity.DatasetVersion))
            {
                datasetVersion ??= identity.DatasetVersion;
                if (!string.Equals(datasetVersion, identity.DatasetVersion, StringComparison.Ordinal))
                {
                    errors.Add(new ImportErrorDto(excelRow, "_dataset_version", "同一工作簿不能混用多个数据集版本。", identity.DatasetVersion));
                }
            }
            if (!string.IsNullOrWhiteSpace(identity.ExportBatchId))
            {
                exportBatchId ??= identity.ExportBatchId;
                if (!string.Equals(exportBatchId, identity.ExportBatchId, StringComparison.Ordinal))
                {
                    errors.Add(new ImportErrorDto(excelRow, "_export_batch_id", "同一工作簿不能混用多个来源导出批次。", identity.ExportBatchId));
                }
            }
        }

        context = new RoundTripWorkbookContext(
            selected,
            identities,
            datasetVersion ?? "1",
            exportBatchId,
            errors);
        return true;
    }

    private static string? ReadValue(Dictionary<string, int> indexes, IReadOnlyList<object?> row, string key) =>
        indexes.TryGetValue(key, out var index) && index < row.Count
            ? ToText(row[index])
            : null;

    private static string? ToText(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();

    private static string ToHeader(object? value) =>
        ToText(value) ?? string.Empty;
}
