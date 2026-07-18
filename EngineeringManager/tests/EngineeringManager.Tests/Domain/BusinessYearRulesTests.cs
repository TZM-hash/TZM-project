using EngineeringManager.Domain.Employees;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class BusinessYearRulesTests
{
    [Fact]
    public void CustomBusinessYearsCannotOverlap()
    {
        var existing = new[]
        {
            new BusinessYearPeriod(new DateOnly(2026, 3, 1), new DateOnly(2027, 2, 28))
        };

        var action = () => BusinessYearRules.EnsureNoOverlap(
            new DateOnly(2027, 2, 28),
            new DateOnly(2028, 2, 29),
            existing);

        action.Should().Throw<InvalidOperationException>().WithMessage("*重叠*");
    }

    [Fact]
    public void WageSegmentMustStayInsideOneBusinessYear()
    {
        var period = new BusinessYearPeriod(new DateOnly(2026, 3, 1), new DateOnly(2027, 2, 28));

        var action = () => BusinessYearRules.EnsureContained(
            new DateOnly(2027, 2, 20),
            new DateOnly(2027, 3, 2),
            period,
            "工资段");

        action.Should().Throw<InvalidOperationException>().WithMessage("*不能跨业务年度*");
    }
}
