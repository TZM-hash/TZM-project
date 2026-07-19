namespace EngineeringManager.Infrastructure.Search;

internal static class SearchTerms
{
    public static string[] Parse(string? search) => string.IsNullOrWhiteSpace(search)
        ? []
        : search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static bool TryParseDate(string term, out DateOnly date) =>
        DateOnly.TryParse(term, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out date)
        || DateOnly.TryParse(term, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);

    public static bool TryParseDecimal(string term, out decimal value) =>
        decimal.TryParse(term, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out value)
        || decimal.TryParse(term, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
}
