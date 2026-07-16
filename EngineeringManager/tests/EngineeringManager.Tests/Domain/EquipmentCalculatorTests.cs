using EngineeringManager.Domain.Equipment;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class EquipmentCalculatorTests
{
    [Fact]
    public void InclusivePeriodsClassifyWorkStopAndUnclassifiedDays()
    {
        var periods = new[]
        {
            new EquipmentUsagePeriodInput(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 4), EquipmentPeriodType.Work, true),
            new EquipmentUsagePeriodInput(new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 7), EquipmentPeriodType.Stop, false)
        };

        var result = EquipmentUsageCalculator.Calculate(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), periods);

        result.TotalDays.Should().Be(10);
        result.WorkDays.Should().Be(4);
        result.StopDays.Should().Be(3);
        result.ChargeableStopDays.Should().Be(0);
        result.UnclassifiedDays.Should().Be(3);
        result.ChargeableDays.Should().Be(4);
    }

    [Fact]
    public void OverlappingOrOutOfRangePeriodsAreRejected()
    {
        var periods = new[]
        {
            new EquipmentUsagePeriodInput(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5), EquipmentPeriodType.Work, true),
            new EquipmentUsagePeriodInput(new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 6), EquipmentPeriodType.Stop, true)
        };

        var action = () => EquipmentUsageCalculator.Calculate(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), periods);

        action.Should().Throw<InvalidOperationException>().WithMessage("*重叠*");
    }

    [Fact]
    public void DailyThirtyDayMonthlyNaturalMonthlyAndPackageRentsAreCalculated()
    {
        var periods = new[]
        {
            new EquipmentUsagePeriodInput(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 30), EquipmentPeriodType.Work, true)
        };
        var usage = EquipmentUsageCalculator.Calculate(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 30), periods);
        var adjustments = new[]
        {
            new EquipmentRentAdjustmentInput(EquipmentAdjustmentDirection.Addition, 100m),
            new EquipmentRentAdjustmentInput(EquipmentAdjustmentDirection.Deduction, 40m)
        };

        EquipmentRentCalculator.Calculate(new EquipmentRentInput(RentMode.Daily, 100m, MonthlyProrationMode.ThirtyDay, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 30), periods, adjustments)).TotalAmount.Should().Be(3060m);
        EquipmentRentCalculator.Calculate(new EquipmentRentInput(RentMode.Monthly, 3000m, MonthlyProrationMode.ThirtyDay, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 30), periods, adjustments)).TotalAmount.Should().Be(3060m);
        EquipmentRentCalculator.Calculate(new EquipmentRentInput(RentMode.Monthly, 3100m, MonthlyProrationMode.CalendarMonth, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), [new(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), EquipmentPeriodType.Work, true)], adjustments)).TotalAmount.Should().Be(3160m);
        EquipmentRentCalculator.Calculate(new EquipmentRentInput(RentMode.StagePackage, 5000m, MonthlyProrationMode.ThirtyDay, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 30), periods, adjustments)).TotalAmount.Should().Be(5060m);
        usage.ChargeableDays.Should().Be(30);
    }
}
