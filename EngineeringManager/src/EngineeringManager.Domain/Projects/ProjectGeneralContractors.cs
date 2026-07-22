using System.Text.Json;

namespace EngineeringManager.Domain.Projects;

public static class ProjectGeneralContractors
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly string[] CompanySuffixes =
    [
        "股份有限公司",
        "有限责任公司",
        "集团有限公司",
        "有限公司",
        "集团公司",
        "公司"
    ];

    public static IReadOnlyList<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var text = raw.Trim();
        if (text.StartsWith('['))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<string>>(text, JsonOptions) ?? [];
                return Normalize(items);
            }
            catch (JsonException)
            {
                // fall through to plain text
            }
        }

        return Normalize([text]);
    }

    public static string? Serialize(IEnumerable<string?>? names)
    {
        var items = Normalize(names ?? []);
        if (items.Count == 0)
        {
            return null;
        }

        if (items.Count == 1)
        {
            return items[0];
        }

        return JsonSerializer.Serialize(items, JsonOptions);
    }

    public static string Display(string? raw, int maxLength = 28)
    {
        var items = Parse(raw);
        if (items.Count == 0)
        {
            return "未设置";
        }

        var full = string.Join("，", items);
        if (full.Length <= maxLength)
        {
            return full;
        }

        var shortNames = items.Select(ToShortName).ToArray();
        var compact = string.Join("，", shortNames);
        if (compact.Length <= maxLength)
        {
            return compact;
        }

        // Keep every contractor visible: share the budget across short names.
        var separators = Math.Max(0, shortNames.Length - 1);
        var budget = Math.Max(shortNames.Length, maxLength - separators);
        var baseLen = Math.Max(1, budget / shortNames.Length);
        var remainder = budget % shortNames.Length;
        var parts = new string[shortNames.Length];
        for (var index = 0; index < shortNames.Length; index++)
        {
            var allow = baseLen + (index < remainder ? 1 : 0);
            var name = shortNames[index];
            parts[index] = name.Length <= allow ? name : name[..allow];
        }

        return string.Join("，", parts);
    }

    public static string? Primary(string? raw)
    {
        var items = Parse(raw);
        return items.Count == 0 ? null : items[0];
    }

    public static string ToShortName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return value;
        }

        foreach (var suffix in CompanySuffixes)
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal) && value.Length > suffix.Length + 1)
            {
                value = value[..^suffix.Length];
                break;
            }
        }

        return value.Trim();
    }

    private static List<string> Normalize(IEnumerable<string?> names)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var value = name?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(value);
            if (result.Count >= 3)
            {
                break;
            }
        }

        return result;
    }
}
