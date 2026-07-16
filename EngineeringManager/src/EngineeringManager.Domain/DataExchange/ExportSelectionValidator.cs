namespace EngineeringManager.Domain.DataExchange;

public static class ExportSelectionValidator
{
    public static IReadOnlyList<ExportFieldDefinition> ResolveFields(
        IReadOnlyList<ExportFieldDefinition> availableFields,
        IReadOnlyList<string> selectedKeys)
    {
        ArgumentNullException.ThrowIfNull(availableFields);
        ArgumentNullException.ThrowIfNull(selectedKeys);
        if (availableFields.Count == 0)
        {
            throw new ArgumentException("导出数据集没有可用字段。", nameof(availableFields));
        }

        var available = availableFields.ToDictionary(item => item.Key, StringComparer.Ordinal);
        if (selectedKeys.Count == 0)
        {
            var defaults = availableFields.Where(item => item.IsDefault).ToArray();
            if (defaults.Length == 0)
            {
                throw new ArgumentException("导出数据集没有默认字段。", nameof(availableFields));
            }

            return defaults;
        }

        if (selectedKeys.Distinct(StringComparer.Ordinal).Count() != selectedKeys.Count)
        {
            throw new ArgumentException("导出字段不能重复。", nameof(selectedKeys));
        }

        var resolved = new List<ExportFieldDefinition>(selectedKeys.Count);
        foreach (var key in selectedKeys)
        {
            if (!available.TryGetValue(key, out var field))
            {
                throw new ArgumentException($"未知导出字段：{key}", nameof(selectedKeys));
            }

            resolved.Add(field);
        }

        return resolved;
    }

    public static void ValidateTemplate(ExportTemplateScope scope, string ownerUserId, bool canPublishShared)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException("导出模板必须记录创建用户。", nameof(ownerUserId));
        }

        if (scope == ExportTemplateScope.Shared && !canPublishShared)
        {
            throw new InvalidOperationException("只有管理员可以发布共享导出模板。");
        }
    }

    public static void ValidateCutoffDate(DateOnly? cutoffDate)
    {
        _ = cutoffDate;
    }
}
