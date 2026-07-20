using EngineeringManager.Domain.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class ProjectAmountCalculatorTests
{
    [Fact]
    public void TreatsPartiallySettledStageAsSettlementAmount()
    {
        var summary = ProjectAmountCalculator.Calculate(ProjectStage.PartiallySettled,
        [
            new LineItemAmountInput(10m, 5m, true),
            new LineItemAmountInput(4m, 8m, false)
        ]);

        summary.EstimatedAmount.Should().Be(0m);
        summary.SettledAmount.Should().Be(82m);
        summary.CurrentAmount.Should().Be(82m);
        summary.InvoiceRequiredAmount.Should().Be(50m);
        summary.SettlementStatus.Should().Be(ProjectSettlementStatus.PartiallySettled);
    }

    [Fact]
    public void TreatsUnsettledStageAsEstimatedAmount()
    {
        var summary = ProjectAmountCalculator.Calculate(ProjectStage.UnderConstruction,
        [
            new LineItemAmountInput(10m, 5m, true),
            new LineItemAmountInput(null, 8m, true)
        ]);

        summary.EstimatedAmount.Should().Be(50m);
        summary.SettledAmount.Should().Be(0m);
        summary.CurrentAmount.Should().Be(50m);
        summary.InvoiceRequiredAmount.Should().Be(50m);
        summary.SettlementStatus.Should().Be(ProjectSettlementStatus.Estimated);
    }

    [Fact]
    public void TreatsArchivedSettlementStageAsFinalSettlement()
    {
        var summary = ProjectAmountCalculator.Calculate(ProjectStage.SettledArchived,
        [
            new LineItemAmountInput(9m, 5.5m, true),
            new LineItemAmountInput(4m, 8m, true)
        ]);

        summary.CurrentAmount.Should().Be(81.5m);
        summary.SettlementStatus.Should().Be(ProjectSettlementStatus.Settled);
    }
}
