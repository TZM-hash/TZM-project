namespace EngineeringManager.Domain.Equipment;

public static class EquipmentRentCalculator
{
    public static EquipmentRentCalculation Calculate(EquipmentRentInput input)
    {
        if (input.UnitRate < 0m) throw new ArgumentOutOfRangeException(nameof(input), "基础单价不能为负数。");
        var usage = EquipmentUsageCalculator.Calculate(input.EntryDate, input.ExitDate, input.Periods);
        var baseAmount = input.RentMode switch
        {
            RentMode.Daily => input.UnitRate * usage.ChargeableDays,
            RentMode.Monthly when input.MonthlyProrationMode == MonthlyProrationMode.ThirtyDay => input.UnitRate / 30m * usage.ChargeableDays,
            RentMode.Monthly => CalculateCalendarMonth(input),
            RentMode.StagePackage => input.UnitRate,
            _ => throw new ArgumentOutOfRangeException(nameof(input), "未知计租方式。")
        };
        var additions = input.Adjustments.Where(item => item.Direction == EquipmentAdjustmentDirection.Addition).Sum(item => ValidateAmount(item.Amount));
        var deductions = input.Adjustments.Where(item => item.Direction == EquipmentAdjustmentDirection.Deduction).Sum(item => ValidateAmount(item.Amount));
        return new EquipmentRentCalculation(
            decimal.Round(baseAmount, 2, MidpointRounding.AwayFromZero),
            additions,
            deductions,
            decimal.Round(baseAmount + additions - deductions, 2, MidpointRounding.AwayFromZero));
    }

    private static decimal CalculateCalendarMonth(EquipmentRentInput input)
    {
        var amount = 0m;
        foreach (var period in input.Periods.Where(item => item.PeriodType == EquipmentPeriodType.Work || item.IsChargeable))
        {
            for (var date = period.StartDate; date <= period.EndDate; date = date.AddDays(1))
            {
                amount += input.UnitRate / DateTime.DaysInMonth(date.Year, date.Month);
            }
        }
        return amount;
    }

    private static decimal ValidateAmount(decimal amount) => amount < 0m
        ? throw new ArgumentOutOfRangeException(nameof(amount), "加减项金额不能为负数。")
        : amount;
}
