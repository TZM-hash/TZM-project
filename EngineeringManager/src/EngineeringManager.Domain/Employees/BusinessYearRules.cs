namespace EngineeringManager.Domain.Employees;

public sealed record BusinessYearPeriod
{
    public BusinessYearPeriod(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("业务年度结束日期不能早于开始日期。", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
}

public static class BusinessYearRules
{
    public static void EnsureNoOverlap(
        DateOnly startDate,
        DateOnly endDate,
        IEnumerable<BusinessYearPeriod> existingPeriods)
    {
        ArgumentNullException.ThrowIfNull(existingPeriods);
        var candidate = new BusinessYearPeriod(startDate, endDate);
        if (existingPeriods.Any(existing =>
                candidate.StartDate <= existing.EndDate && existing.StartDate <= candidate.EndDate))
        {
            throw new InvalidOperationException("业务年度日期范围不能与已有年度重叠。");
        }
    }

    public static void EnsureContained(
        DateOnly startDate,
        DateOnly endDate,
        BusinessYearPeriod businessYear,
        string subjectName)
    {
        ArgumentNullException.ThrowIfNull(businessYear);
        if (endDate < startDate)
        {
            throw new ArgumentException($"{subjectName}结束日期不能早于开始日期。", nameof(endDate));
        }

        if (!businessYear.Contains(startDate) || !businessYear.Contains(endDate))
        {
            throw new InvalidOperationException($"{subjectName}不能跨业务年度。");
        }
    }
}
