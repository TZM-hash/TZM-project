using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Integration;

public sealed class EndToEndBusinessFlowTests
{
    [Fact]
    public void ProjectEstimateSettlementAndEquipmentCostUseConfirmedBusinessRules()
    {
        var project = ProjectAmountCalculator.Calculate([new LineItemAmountInput(10m, 120m, 9m, 125m)]);
        var equipment = EquipmentRentCalculator.Calculate(new EquipmentRentInput(RentMode.Daily, 800m, MonthlyProrationMode.ThirtyDay, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3), [new(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3), EquipmentPeriodType.Work, true)], []));
        project.EstimatedAmount.Should().Be(1200m);
        project.SettledAmount.Should().Be(1125m);
        equipment.TotalAmount.Should().Be(2400m);
    }
}
