using System.Text.Json;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.Settings;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.DataViews;

public sealed class SavedDataViewService(ApplicationDbContext db) : ISavedDataViewService
{
    private static readonly HashSet<int> AllowedPageSizes = [20, 50, 100];

    public async Task<IReadOnlyList<SavedDataViewDto>> ListAsync(string userId, DataViewDefinition definition, CancellationToken token)
    {
        ValidateIdentity(userId, definition.PageKey);
        var entities = await db.SavedDataViews.AsNoTracking()
            .Where(item => item.UserId == userId && item.PageKey == definition.PageKey)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .ToListAsync(token);
        return entities.Select(item => ToDto(item, definition)).ToArray();
    }

    public async Task<SavedDataViewDto> SaveAsync(string userId, SaveDataViewRequest request, DataViewDefinition definition, CancellationToken token)
    {
        ValidateIdentity(userId, definition.PageKey);
        if (!string.Equals(request.PageKey, definition.PageKey, StringComparison.Ordinal)) throw new ArgumentException("页面键与视图定义不一致。", nameof(request));
        var name = request.Name.Trim();
        if (name.Length is < 1 or > 100) throw new ArgumentException("视图名称长度必须为 1～100 个字符。", nameof(request));
        if (!AllowedPageSizes.Contains(request.PageSize)) throw new ArgumentOutOfRangeException(nameof(request), "每页数量只能为 20、50 或 100。");
        if (!Enum.IsDefined(request.RowDensity)) throw new ArgumentOutOfRangeException(nameof(request), "未知表格密度。");

        var filterJson = SanitizeFilters(request.FilterJson, definition.FilterKeys);
        var columnJson = SanitizeColumns(request.ColumnJson, definition.ColumnKeys);
        var sortKey = request.SortKey is not null && definition.SortKeys.Contains(request.SortKey) ? request.SortKey : null;
        if (request.IsDefault)
        {
            var defaults = await db.SavedDataViews.Where(item => item.UserId == userId && item.PageKey == definition.PageKey && item.IsDefault).ToListAsync(token);
            foreach (var item in defaults) item.IsDefault = false;
        }

        SavedDataView? entity = null;
        if (request.Id.HasValue)
        {
            entity = await db.SavedDataViews.SingleOrDefaultAsync(item => item.Id == request.Id && item.UserId == userId, token);
        }
        entity ??= await db.SavedDataViews.SingleOrDefaultAsync(item => item.UserId == userId && item.PageKey == definition.PageKey && item.Name == name, token);
        if (entity is null)
        {
            entity = new SavedDataView { UserId = userId, PageKey = definition.PageKey, Name = name };
            db.SavedDataViews.Add(entity);
        }
        entity.Name = name;
        entity.IsDefault = request.IsDefault;
        entity.FilterJson = filterJson;
        entity.ColumnJson = columnJson;
        entity.SortKey = sortKey;
        entity.SortDescending = sortKey is not null && request.SortDescending;
        entity.RowDensity = request.RowDensity;
        entity.PageSize = request.PageSize;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "SaveDataView", EntityType = "SavedDataView", EntityId = entity.Id.ToString(), Reason = $"保存个人视图：{name}", AfterJson = JsonSerializer.Serialize(new { entity.PageKey, entity.Name, entity.IsDefault, entity.FilterJson, entity.ColumnJson, entity.SortKey, entity.SortDescending, entity.RowDensity, entity.PageSize }) });
        await db.SaveChangesAsync(token);
        return ToDto(entity, definition);
    }

    public async Task DeleteAsync(string userId, Guid id, CancellationToken token)
    {
        var entity = await db.SavedDataViews.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, token)
            ?? throw new KeyNotFoundException("个人视图不存在。");
        db.SavedDataViews.Remove(entity);
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "DeleteDataView", EntityType = "SavedDataView", EntityId = entity.Id.ToString(), Reason = $"删除个人视图：{entity.Name}", BeforeJson = JsonSerializer.Serialize(new { entity.PageKey, entity.Name, entity.IsDefault }) });
        await db.SaveChangesAsync(token);
    }

    private static SavedDataViewDto ToDto(SavedDataView entity, DataViewDefinition definition) => new(
        entity.Id,
        entity.PageKey,
        entity.Name,
        entity.IsDefault,
        SanitizeFilters(entity.FilterJson, definition.FilterKeys),
        SanitizeColumns(entity.ColumnJson, definition.ColumnKeys),
        entity.SortKey is not null && definition.SortKeys.Contains(entity.SortKey) ? entity.SortKey : null,
        entity.SortDescending,
        Enum.IsDefined(entity.RowDensity) ? entity.RowDensity : TableDensity.Standard,
        AllowedPageSizes.Contains(entity.PageSize) ? entity.PageSize : 20,
        entity.UpdatedAt);

    private static string SanitizeFilters(string json, IReadOnlySet<string> allowedKeys)
    {
        if (json.Length > 8000) throw new ArgumentException("筛选条件过长。", nameof(json));
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return "{}";
            var values = document.RootElement.EnumerateObject()
                .Where(item => allowedKeys.Contains(item.Name))
                .ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
            return JsonSerializer.Serialize(values);
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    private static string SanitizeColumns(string json, IReadOnlySet<string> allowedKeys)
    {
        if (json.Length > 8000) throw new ArgumentException("列配置过长。", nameof(json));
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return "[]";
            var columns = document.RootElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => item is not null && allowedKeys.Contains(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return JsonSerializer.Serialize(columns);
        }
        catch (JsonException)
        {
            return "[]";
        }
    }

    private static void ValidateIdentity(string userId, string pageKey)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("用户标识不能为空。", nameof(userId));
        if (string.IsNullOrWhiteSpace(pageKey) || pageKey.Length > 100) throw new ArgumentException("页面键无效。", nameof(pageKey));
    }
}
