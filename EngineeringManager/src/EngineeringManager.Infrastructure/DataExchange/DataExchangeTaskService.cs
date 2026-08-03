using System.Globalization;
using System.Text.Json;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class DataExchangeTaskService(ApplicationDbContext db) : IDataExchangeTaskService
{
    private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<DataExchangeTaskPageDto> ListAsync(
        DataExchangeTaskQuery query,
        CancellationToken cancellationToken)
    {
        var userId = NormalizeRequired(query.UserId, nameof(query.UserId));
        var pageSize = NormalizePageSize(query.PageSize);
        var page = Math.Max(query.Page, 1);

        var exportQuery = db.DataExchangeTasks.AsNoTracking()
            .Where(item => item.Direction == DataExchangeDirection.Export);
        var importQuery = db.ImportBatches.AsNoTracking();
        if (!query.CanManage)
        {
            exportQuery = exportQuery.Where(item => item.UserId == userId);
            importQuery = importQuery.Where(item => item.CreatedByUserId == userId);
        }

        var exportRows = query.Direction is null or DataExchangeDirection.Export
            ? await exportQuery.Select(item => new ExportTaskProjection(
                item.Id,
                item.UserId,
                item.DatasetsJson,
                item.Status,
                item.RowCount,
                item.FileName,
                item.ErrorMessage,
                item.CreatedAt,
                item.CompletedAt,
                item.Scope,
                item.PackageFormat,
                item.ResultContent != null)).ToListAsync(cancellationToken)
            : [];

        var importRows = query.Direction is null or DataExchangeDirection.Import
            ? await importQuery.Select(item => new ImportTaskProjection(
                item.Id,
                item.CreatedByUserId,
                item.Dataset,
                item.Mode,
                item.OriginalFileName,
                item.Status,
                item.TotalRows,
                item.ErrorRows,
                item.CreatedAt,
                item.CompletedAt)).ToListAsync(cancellationToken)
            : [];

        var all = exportRows.Select(item => new DataExchangeTaskItemDto(
                item.Id,
                DataExchangeDirection.Export,
                item.UserId,
                DeserializeDatasets(item.DatasetsJson),
                item.Status,
                item.RowCount,
                0,
                item.FileName,
                item.ErrorMessage,
                item.CreatedAt,
                item.CompletedAt,
                item.Scope,
                item.PackageFormat,
                null,
                null,
                item.HasContent && item.Status == DataExchangeTaskStatus.Completed,
                false))
            .Concat(importRows.Select(item => new DataExchangeTaskItemDto(
                item.Id,
                DataExchangeDirection.Import,
                item.UserId,
                [item.Dataset],
                item.Status,
                item.TotalRows,
                item.ErrorRows,
                item.OriginalFileName,
                item.ErrorRows > 0 ? $"存在 {item.ErrorRows} 个错误" : null,
                item.CreatedAt,
                item.CompletedAt,
                null,
                null,
                item.Mode,
                ImportSourceType.ExternalWorkbook,
                false,
                item.ErrorRows > 0)))
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToArray();

        var totalPages = all.Length == 0 ? 1 : (int)Math.Ceiling(all.Length / (double)pageSize);
        var effectivePage = Math.Min(page, totalPages);
        var items = all.Skip((effectivePage - 1) * pageSize).Take(pageSize).ToArray();
        return new DataExchangeTaskPageDto(items, effectivePage, pageSize, all.Length, totalPages);
    }

    public async Task<ExportFileResult> DownloadExportAsync(
        string userId,
        bool canManage,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var task = await db.DataExchangeTasks.AsNoTracking()
            .Where(item => item.Id == taskId && item.Direction == DataExchangeDirection.Export)
            .Select(item => new ExportTaskDownloadProjection(
                item.UserId,
                item.Status,
                item.FileName,
                item.ContentType,
                item.ResultContent,
                item.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("导出任务不存在。");
        EnsureOwnerOrManager(task.UserId, normalizedUserId, canManage);
        if (task.Status != DataExchangeTaskStatus.Completed || task.ResultContent is null)
        {
            throw new InvalidOperationException("导出任务尚未生成可下载文件。");
        }

        return new ExportFileResult(
            task.FileName ?? $"数据交换_{task.CreatedAt.LocalDateTime:yyyyMMddHHmmss}.xlsx",
            task.ContentType ?? WorkbookContentType,
            task.ResultContent);
    }

    public async Task<ExportFileResult> DownloadImportErrorsAsync(
        string userId,
        bool canManage,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var batch = await db.ImportBatches.AsNoTracking()
            .Where(item => item.Id == batchId)
            .Select(item => new ImportBatchDownloadProjection(
                item.Id,
                item.CreatedByUserId,
                item.Dataset,
                item.Mode,
                item.OriginalFileName,
                item.Status,
                item.TotalRows,
                item.ErrorRows,
                item.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("导入批次不存在。");
        EnsureOwnerOrManager(batch.CreatedByUserId, normalizedUserId, canManage);

        var errors = await db.ImportErrors.AsNoTracking()
            .Where(item => item.ImportBatchId == batch.Id)
            .OrderBy(item => item.RowNumber)
            .ThenBy(item => item.ColumnName)
            .Select(item => new ImportErrorDownloadProjection(item.RowNumber, item.ColumnName, item.RawValue, item.Message))
            .ToListAsync(cancellationToken);

        var workbook = new SimpleXlsxWorkbook();
        var errorRows = errors
            .Select(item => (IReadOnlyList<object?>)[item.RowNumber, item.ColumnName, item.RawValue, item.Message])
            .ToArray();
        workbook.AddWorksheet(
            "错误报告",
            ["源行", "源列", "原值", "错误与修正建议"],
            errorRows.Length == 0
                ? [(IReadOnlyList<object?>)["", "", "", "此批次没有校验错误。"]]
                : errorRows);
        workbook.AddWorksheet(
            "批次信息",
            ["项目", "内容"],
            [
                (IReadOnlyList<object?>)["数据集", DataExchangeValueLabels.Dataset(batch.Dataset)],
                (IReadOnlyList<object?>)["原始文件", batch.OriginalFileName],
                (IReadOnlyList<object?>)["导入方式", ImportModeLabel(batch.Mode)],
                (IReadOnlyList<object?>)["状态", TaskStatusLabel(batch.Status)],
                (IReadOnlyList<object?>)["创建时间", batch.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)],
                (IReadOnlyList<object?>)["总行数", batch.TotalRows],
                (IReadOnlyList<object?>)["错误行数", batch.ErrorRows]
            ]);

        var baseName = Path.GetFileNameWithoutExtension(batch.OriginalFileName);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "导入批次";
        foreach (var invalid in Path.GetInvalidFileNameChars()) baseName = baseName.Replace(invalid, '_');
        return new ExportFileResult(
            $"{baseName}_导入错误报告.xlsx",
            WorkbookContentType,
            workbook.ToArray());
    }

    private static int NormalizePageSize(int pageSize) => pageSize is 20 or 50 or 100 ? pageSize : 20;

    private static string NormalizeRequired(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value.Trim();

    private static void EnsureOwnerOrManager(string ownerId, string userId, bool canManage)
    {
        if (!canManage && !string.Equals(ownerId, userId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("只有任务创建者或管理员可以下载该文件。");
        }
    }

    private static ExportDataset[] DeserializeDatasets(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ExportDataset[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ImportModeLabel(ImportMode mode) => mode switch
    {
        ImportMode.New => "仅新增",
        ImportMode.Update => "仅更新",
        ImportMode.Mixed => "新增或更新",
        _ => "未知"
    };

    private static string TaskStatusLabel(DataExchangeTaskStatus status) => status switch
    {
        DataExchangeTaskStatus.Pending => "排队中",
        DataExchangeTaskStatus.PreviewReady => "预览待确认",
        DataExchangeTaskStatus.Running => "处理中",
        DataExchangeTaskStatus.Completed => "已完成",
        DataExchangeTaskStatus.Failed => "失败",
        _ => "未知"
    };

    private sealed record ExportTaskProjection(
        Guid Id,
        string UserId,
        string DatasetsJson,
        DataExchangeTaskStatus Status,
        int RowCount,
        string? FileName,
        string? ErrorMessage,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        ExportScope Scope,
        ExportPackageFormat PackageFormat,
        bool HasContent);

    private sealed record ImportTaskProjection(
        Guid Id,
        string UserId,
        ExportDataset Dataset,
        ImportMode Mode,
        string OriginalFileName,
        DataExchangeTaskStatus Status,
        int TotalRows,
        int ErrorRows,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt);

    private sealed record ExportTaskDownloadProjection(
        string UserId,
        DataExchangeTaskStatus Status,
        string? FileName,
        string? ContentType,
        byte[]? ResultContent,
        DateTimeOffset CreatedAt);

    private sealed record ImportBatchDownloadProjection(
        Guid Id,
        string CreatedByUserId,
        ExportDataset Dataset,
        ImportMode Mode,
        string OriginalFileName,
        DataExchangeTaskStatus Status,
        int TotalRows,
        int ErrorRows,
        DateTimeOffset CreatedAt);

    private sealed record ImportErrorDownloadProjection(
        int RowNumber,
        string ColumnName,
        string? RawValue,
        string Message);
}
