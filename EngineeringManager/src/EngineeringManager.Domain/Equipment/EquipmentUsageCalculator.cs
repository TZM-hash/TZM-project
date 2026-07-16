namespace EngineeringManager.Domain.Equipment;

public static class EquipmentUsageCalculator
{
    public static EquipmentUsageCalculation Calculate(
        DateOnly entryDate,
        DateOnly exitDate,
        IEnumerable<EquipmentUsagePeriodInput> periods)
    {
        if (exitDate < entryDate) throw new ArgumentException("退场日期不能早于进场日期。", nameof(exitDate));
        var items = periods.OrderBy(item => item.StartDate).ThenBy(item => item.EndDate).ToArray();
        DateOnly? previousEnd = null;
        foreach (var item in items)
        {
            if (item.EndDate < item.StartDate) throw new ArgumentException("日期段结束日期不能早于开始日期。", nameof(periods));
            if (item.StartDate < entryDate || item.EndDate > exitDate) throw new InvalidOperationException("施工或停工日期段必须位于进退场范围内。");
            if (previousEnd.HasValue && item.StartDate <= previousEnd.Value) throw new InvalidOperationException("施工或停工日期段不能重叠。");
            previousEnd = item.EndDate;
        }

        var totalDays = exitDate.DayNumber - entryDate.DayNumber + 1;
        var workDays = items.Where(item => item.PeriodType == EquipmentPeriodType.Work).Sum(InclusiveDays);
        var stopDays = items.Where(item => item.PeriodType == EquipmentPeriodType.Stop).Sum(InclusiveDays);
        var chargeableStopDays = items.Where(item => item.PeriodType == EquipmentPeriodType.Stop && item.IsChargeable).Sum(InclusiveDays);
        return new EquipmentUsageCalculation(totalDays, workDays, stopDays, chargeableStopDays,
            totalDays - workDays - stopDays, workDays + chargeableStopDays);
    }

    public static int InclusiveDays(EquipmentUsagePeriodInput period) => period.EndDate.DayNumber - period.StartDate.DayNumber + 1;
}
