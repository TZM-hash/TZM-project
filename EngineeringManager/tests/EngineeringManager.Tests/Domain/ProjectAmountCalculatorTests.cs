using EngineeringManager.Domain.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class ProjectAmountCalculatorTests
{
    [Fact]
    public void CalculatesEstimatedSettledAndCurrentAmountsForPartialSettlement()
    {
        var summary = ProjectAmountCalculator.Calculate(
        [
            new LineItemAmountInput(10m, 5m, null, null),
            new LineItemAmountInput(4m, 8m, 3m, 10m)
        ]);

        summary.EstimatedAmount.Should().Be(82m);
        summary.SettledAmount.Should().Be(30m);
        summary.CurrentAmount.Should().Be(80m);
        summary.SettlementStatus.Should().Be(ProjectSettlementStatus.PartiallySettled);
    }

    [Fact]
    public void MarksAllSettledWhenEveryLineHasFinalQuantityAndPrice()
    {
        var summary = ProjectAmountCalculator.Calculate(
        [
            new LineItemAmountInput(10m, 5m, 9m, 5.5m),
            new LineItemAmountInput(4m, 8m, 4m, 8m)
        ]);

        summary.CurrentAmount.Should().Be(81.5m);
        summary.SettlementStatus.Should().Be(ProjectSettlementStatus.Settled);
    }
}
