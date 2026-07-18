using EngineeringManager.Domain.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class ProjectConstructionCalculatorTests
{
    [Fact]
    public void IncludesEntryAndExitDatesAndSubtractsStopDays()
    {
        var result = ProjectConstructionCalculator.Calculate(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), 2, new DateOnly(2026, 7, 20));
        result.TotalDays.Should().Be(10);
        result.WorkDays.Should().Be(8);
    }

    [Fact]
    public void OpenRecordCalculatesThroughSuppliedToday()
    {
        ProjectConstructionCalculator.Calculate(new DateOnly(2026, 7, 15), null, 1, new DateOnly(2026, 7, 17)).Should().Be(new ProjectConstructionDuration(3, 2));
    }

    [Fact]
    public void RejectsInvalidDatesAndStopDays()
    {
        FluentActions.Invoking(() => ProjectConstructionCalculator.Calculate(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 9), 0, new DateOnly(2026, 7, 17))).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ProjectConstructionCalculator.Calculate(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 10), 2, new DateOnly(2026, 7, 17))).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ProjectConstructionCalculator.Calculate(null, null, 1, new DateOnly(2026, 7, 17))).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ProjectConstructionCalculator.Calculate(null, new DateOnly(2026, 7, 10), 0, new DateOnly(2026, 7, 17))).Should().Throw<ArgumentException>();
    }
}
