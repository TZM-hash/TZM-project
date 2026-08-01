using System.Globalization;

namespace EngineeringManager.Web.Presentation;

public static class ShortBusinessNumber
{
    public static string Next(IEnumerable<string?> existingNumbers, string prefix, int digits = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentOutOfRangeException.ThrowIfLessThan(digits, 1);

        var used = existingNumbers
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Select(number => number!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var sequence = 1; ; sequence++)
        {
            var candidate = prefix + sequence.ToString($"D{digits}", CultureInfo.InvariantCulture);
            if (!used.Contains(candidate)) return candidate;
        }
    }
}
