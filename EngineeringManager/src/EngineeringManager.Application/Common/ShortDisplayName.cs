namespace EngineeringManager.Application.Common;

public static class ShortDisplayName
{
    public const string CopySuffix = "（副本）";

    public static string Copy(string? source, int maxLength)
    {
        if (maxLength <= 0) return string.Empty;

        var value = source?.Trim() ?? string.Empty;
        var baseName = value.EndsWith(CopySuffix, StringComparison.Ordinal)
            ? value[..^CopySuffix.Length].TrimEnd()
            : value;
        var baseLength = Math.Max(0, maxLength - CopySuffix.Length);
        if (baseName.Length > baseLength)
            baseName = baseName[..baseLength];
        return baseName + CopySuffix;
    }
}
