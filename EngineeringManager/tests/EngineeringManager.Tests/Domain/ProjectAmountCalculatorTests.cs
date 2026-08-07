using EngineeringManager.Domain.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class ProjectAmountCalculatorTests
{
    [Fact]
    public void TreatsPartiallySettledStageAsUnifiedCurrentAmount()
    {
        var summary = ProjectAmountCalculator.Calculate(ProjectStage.PartiallySettled,
        [
            new LineItemAmountInput(10m, 5m, true),
            new LineItemAmountInput(4m, 8m, false)
        ]);

        summary.CurrentAmount.Should().Be(82m);
        summary.InvoiceRequiredAmount.Should().Be(50m);
        summary.SettlementStatus.Should().Be(ProjectSettlementStatus.PartiallySettled);
    }

    [Fact]
    public void TreatsUnsettledStageAsUnifiedCurrentAmount()
    {
        var summary = ProjectAmountCalculator.Calculate(ProjectStage.UnderConstruction,
        [
            new LineItemAmountInput(10m, 5m, true),
            new LineItemAmountInput(null, 8m, true)
        ]);

        summary.CurrentAmount.Should().Be(50m);
        summary.InvoiceRequiredAmount.Should().Be(50m);
        summary.SettlementStatus.Should().Be(ProjectSettlementStatus.Estimated);
    }

    [Fact]
    public void ProjectAmountSummaryExposesNoRedundantAmountBuckets()
    {
        typeof(ProjectAmountSummary).GetProperty("EstimatedAmount").Should().BeNull();
        typeof(ProjectAmountSummary).GetProperty("SettledAmount").Should().BeNull();
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
