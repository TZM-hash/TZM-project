namespace EngineeringManager.Domain.Projects;

public static class ProjectConstructionCalculator
{
    public static ProjectConstructionDuration Calculate(DateOnly? entryDate, DateOnly? exitDate, int stopDays, DateOnly today)
    {
        if (!entryDate.HasValue)
        {
            if (exitDate.HasValue) throw new ArgumentException("填写退场日期前必须先填写进场日期。");
            if (stopDays != 0) throw new ArgumentException("未填写进场日期时停工天数必须为 0。");
            return new ProjectConstructionDuration(0, 0);
        }
        var end = exitDate ?? today;
        if (end < entryDate.Value) throw new ArgumentException("退场日期不得早于进场日期。");
        var totalDays = end.DayNumber - entryDate.Value.DayNumber + 1;
        if (stopDays < 0 || stopDays > totalDays) throw new ArgumentException("停工天数必须在 0 至总天数之间。");
        return new ProjectConstructionDuration(totalDays, totalDays - stopDays);
    }
}

public readonly record struct ProjectConstructionDuration(int TotalDays, int WorkDays);
