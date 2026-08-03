using System.Globalization;
using System.Text.RegularExpressions;

namespace EngineeringManager.Infrastructure.Data;

public enum ProjectNoteDateEventKind
{
    Start = 0,
    Completion = 1
}

public sealed record ProjectNoteDateCandidate(
    DateOnly Date,
    ProjectNoteDateEventKind Kind,
    string RawText,
    string Context);

public sealed record ProjectNoteDateParseResult(
    DateOnly? StartDate,
    DateOnly? CompletionDate,
    IReadOnlyList<ProjectNoteDateCandidate> Candidates,
    IReadOnlyList<string> Warnings)
{
    public bool HasUnsafeOrdering => StartDate.HasValue
        && CompletionDate.HasValue
        && CompletionDate.Value < StartDate.Value;
}

public static class ProjectNoteDateParser
{
    private const int ContextRadius = 70;
    private const int SignalRadius = 48;

    private static readonly Regex FullDateRegex = new(
        "(?<!\\d)(?:(?<year>19\\d{2}|20\\d{2})\\s*(?:年|[./-])\\s*(?<month>0?[1-9]|1[0-2])\\s*(?:月|[./-])\\s*(?<day>0?[1-9]|[12]\\d|3[01])\\s*(?:日|号)?|(?<compact>(?:19|20)\\d{6})|(?<year2>19\\d{2}|20\\d{2})(?<month2>0?[1-9]|1[0-2])\\s*[./-]\\s*(?<day2>0?[1-9]|[12]\\d|3[01]))(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex YearlessDateRegex = new(
        "(?<![\\d./-])(?<month>0?[1-9]|1[0-2])\\s*(?:月|[./-])\\s*(?<day>0?[1-9]|[12]\\d|3[01])\\s*(?:日|号)?(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StartSignalRegex = new(
        "进场(?!费)|入场|到场|进驻|进第一台|开始施工|开工",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CompletionSignalRegex = new(
        "退场|撤场|退租|出场|离场|完工|施工完成|结束|转卖|转让|收工",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FinancialSignalRegex = new(
        "打款|转账|付款|支付|收款|开票|发票|税金|保证金|工资|利息|银行|退回|工程款|租金|付给",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MachineSignalRegex = new(
        "旋挖|挖机|钻机|号机|机器|机械|设备|租赁|班组|钻杆|打桩|桩机|护筒",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ProjectNoteDateParseResult Parse(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return new(null, null, [], []);

        var candidates = new List<ProjectNoteDateCandidate>();
        var warnings = new List<string>();
        var matchedRanges = new List<(int Start, int Length)>();

        foreach (Match match in FullDateRegex.Matches(notes))
        {
            if (!TryParseDate(match, out var date))
                continue;

            matchedRanges.Add((match.Index, match.Length));
            var context = GetContext(notes, match.Index, match.Length);
            var kind = Classify(notes, match.Index, match.Length, context);
            if (!kind.HasValue)
                continue;

            candidates.Add(new ProjectNoteDateCandidate(date, kind.Value, match.Value, context));
        }

        foreach (Match match in YearlessDateRegex.Matches(notes))
        {
            if (matchedRanges.Any(item => match.Index >= item.Start && match.Index < item.Start + item.Length))
                continue;

            var context = GetContext(notes, match.Index, match.Length);
            if (Classify(notes, match.Index, match.Length, context).HasValue)
                warnings.Add($"发现无年份日期“{match.Value}”，无法安全推导项目年份。");
        }

        var startDate = candidates
            .Where(item => item.Kind == ProjectNoteDateEventKind.Start)
            .Select(item => (DateOnly?)item.Date)
            .Min();
        var completionDate = candidates
            .Where(item => item.Kind == ProjectNoteDateEventKind.Completion)
            .Select(item => (DateOnly?)item.Date)
            .Max();

        if (startDate.HasValue && completionDate.HasValue && completionDate.Value < startDate.Value)
            warnings.Add($"候选完工日期 {completionDate:yyyy-MM-dd} 早于候选开工日期 {startDate:yyyy-MM-dd}，待人工核实。");

        return new ProjectNoteDateParseResult(
            startDate,
            completionDate,
            candidates.OrderBy(item => item.Date).ThenBy(item => item.Kind).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static ProjectNoteDateEventKind? Classify(string notes, int dateIndex, int dateLength, string context)
    {
        var lifecycleAfter = FindNearestLifecycleSignalAfter(notes, dateIndex, dateLength);
        var financialAfter = FindNearestAfter(FinancialSignalRegex, notes, dateIndex, dateLength);
        if (financialAfter.HasValue && (!lifecycleAfter.HasValue || financialAfter.Value < lifecycleAfter.Value.Distance))
            return null;

        if (lifecycleAfter is { Distance: <= SignalRadius })
            return lifecycleAfter.Value.Kind;

        var lifecycleBefore = FindNearestLifecycleSignalBefore(notes, dateIndex);
        var nearestFinancial = FindNearest(FinancialSignalRegex, notes, dateIndex, dateLength);
        var nearestLifecycle = lifecycleBefore?.Distance ?? int.MaxValue;

        if (nearestFinancial.HasValue && nearestFinancial.Value <= nearestLifecycle)
            return null;

        if (lifecycleBefore is { Distance: <= SignalRadius })
            return lifecycleBefore.Value.Kind;

        if (MachineSignalRegex.IsMatch(context) && !FinancialSignalRegex.IsMatch(context))
            return ProjectNoteDateEventKind.Start;

        return null;
    }

    private static (ProjectNoteDateEventKind Kind, int Distance)? FindNearestLifecycleSignalAfter(
        string notes,
        int dateIndex,
        int dateLength)
    {
        var dateEnd = dateIndex + dateLength;
        var after = StartSignalRegex.Matches(notes)
            .Cast<Match>()
            .Select(match => (Kind: ProjectNoteDateEventKind.Start, Index: match.Index, Length: match.Length))
            .Concat(CompletionSignalRegex.Matches(notes)
                .Cast<Match>()
                .Select(match => (Kind: ProjectNoteDateEventKind.Completion, Index: match.Index, Length: match.Length)))
            .Where(match => match.Index >= dateEnd && match.Index - dateEnd <= SignalRadius)
            .OrderBy(match => match.Index - dateEnd)
            .FirstOrDefault();
        return after.Length > 0
            ? (after.Kind, after.Index - dateEnd)
            : null;
    }

    private static (ProjectNoteDateEventKind Kind, int Distance)? FindNearestLifecycleSignalBefore(
        string notes,
        int dateIndex)
    {
        var before = StartSignalRegex.Matches(notes)
            .Cast<Match>()
            .Select(match => (Kind: ProjectNoteDateEventKind.Start, Index: match.Index, Length: match.Length))
            .Concat(CompletionSignalRegex.Matches(notes)
                .Cast<Match>()
                .Select(match => (Kind: ProjectNoteDateEventKind.Completion, Index: match.Index, Length: match.Length)))
            .Where(match => match.Index < dateIndex && dateIndex - (match.Index + match.Length) <= SignalRadius)
            .OrderBy(match => dateIndex - (match.Index + match.Length))
            .FirstOrDefault();
        return before.Length > 0
            ? (before.Kind, dateIndex - (before.Index + before.Length))
            : null;
    }

    private static int? FindNearestAfter(Regex regex, string notes, int dateIndex, int dateLength)
    {
        var dateEnd = dateIndex + dateLength;
        var distance = regex.Matches(notes)
            .Cast<Match>()
            .Select(match => match.Index - dateEnd)
            .Where(value => value >= 0 && value <= ContextRadius)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        return distance == int.MaxValue ? null : distance;
    }

    private static int? FindNearest(Regex regex, string notes, int dateIndex, int dateLength)
    {
        var windowStart = Math.Max(0, dateIndex - ContextRadius);
        var windowEnd = Math.Min(notes.Length, dateIndex + dateLength + ContextRadius);
        var window = notes[windowStart..windowEnd];
        var distances = regex.Matches(window)
            .Select(match => Math.Abs((windowStart + match.Index) - dateIndex))
            .Where(distance => distance <= ContextRadius)
            .ToArray();
        return distances.Length == 0 ? null : distances.Min();
    }

    private static string GetContext(string notes, int index, int length)
    {
        var start = Math.Max(0, index - ContextRadius);
        var end = Math.Min(notes.Length, index + length + ContextRadius);
        return notes[start..end].Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static bool TryParseDate(Match match, out DateOnly date)
    {
        date = default;
        try
        {
            if (match.Groups["compact"].Success)
            {
                var value = match.Groups["compact"].Value;
                date = new DateOnly(
                    int.Parse(value[..4], CultureInfo.InvariantCulture),
                    int.Parse(value.AsSpan(4, 2), CultureInfo.InvariantCulture),
                    int.Parse(value.AsSpan(6, 2), CultureInfo.InvariantCulture));
                return true;
            }

            var yearGroup = match.Groups["year"].Success ? match.Groups["year"] : match.Groups["year2"];
            var monthGroup = match.Groups["month"].Success ? match.Groups["month"] : match.Groups["month2"];
            var dayGroup = match.Groups["day"].Success ? match.Groups["day"] : match.Groups["day2"];
            date = new DateOnly(
                int.Parse(yearGroup.Value, CultureInfo.InvariantCulture),
                int.Parse(monthGroup.Value, CultureInfo.InvariantCulture),
                int.Parse(dayGroup.Value, CultureInfo.InvariantCulture));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
