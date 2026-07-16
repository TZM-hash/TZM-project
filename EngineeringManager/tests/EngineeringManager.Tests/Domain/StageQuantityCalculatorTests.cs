using EngineeringManager.Domain.StageResults;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class StageQuantityCalculatorTests
{
    [Fact]
    public void CalculatesCumulativeRemainingAndPercentage()
    {
        var result = StageQuantityCalculator.Calculate(100m, 30m, 20m);

        result.CumulativeQuantity.Should().Be(50m);
        result.RemainingQuantity.Should().Be(50m);
        result.CompletionPercentage.Should().Be(50m);
        result.ExceedsTarget.Should().BeFalse();
    }

    [Fact]
    public void KeepsOverTargetQuantityAndMarksRisk()
    {
        var result = StageQuantityCalculator.Calculate(100m, 90m, 20m);

        result.CumulativeQuantity.Should().Be(110m);
        result.RemainingQuantity.Should().Be(0m);
        result.CompletionPercentage.Should().Be(110m);
        result.ExceedsTarget.Should().BeTrue();
    }

    [Fact]
    public void RejectsNegativeQuantities()
    {
        var action = () => StageQuantityCalculator.Calculate(100m, 20m, -1m);

        action.Should().Throw<ArgumentException>().WithMessage("*工程量*");
    }
}
