using EngineeringManager.Infrastructure.Data;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectNoteDateParserTests
{
    [Fact]
    public void ParseUsesEarliestMachineEntryAndLatestMachineExit()
    {
        var result = ProjectNoteDateParser.Parse("405 9号机，2025.3.10进场，2025.10.16退场\n山河240，2025年5月11日进场，2025/9/7退场");

        result.StartDate.Should().Be(new DateOnly(2025, 3, 10));
        result.CompletionDate.Should().Be(new DateOnly(2025, 10, 16));
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ParseSupportsCompactDatesAndCompletionWords()
    {
        var result = ProjectNoteDateParser.Parse("旋挖机 20240617进场，2024-08-18完工；机器转卖");

        result.StartDate.Should().Be(new DateOnly(2024, 6, 17));
        result.CompletionDate.Should().Be(new DateOnly(2024, 8, 18));
    }

    [Fact]
    public void ParseExcludesFinancialDates()
    {
        var result = ProjectNoteDateParser.Parse("240机 2025.3.10进场，2025.10.16退场\n2025.11.01转账付款，2025.12.01开票");

        result.StartDate.Should().Be(new DateOnly(2025, 3, 10));
        result.CompletionDate.Should().Be(new DateOnly(2025, 10, 16));
        result.Candidates.Should().NotContain(item => item.Date == new DateOnly(2025, 11, 1) || item.Date == new DateOnly(2025, 12, 1));
    }

    [Fact]
    public void ParseDoesNotGuessYearForMonthAndDayOnlyDates()
    {
        var result = ProjectNoteDateParser.Parse("240E机4月27日进场，7月5日工程完工");

        result.StartDate.Should().BeNull();
        result.CompletionDate.Should().BeNull();
        result.Warnings.Should().Contain(item => item.Contains("年份", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseMarksCompletionBeforeStartAsUnsafe()
    {
        var result = ProjectNoteDateParser.Parse("山河240，2025.10.5进场，2025.2.1完工");

        result.StartDate.Should().Be(new DateOnly(2025, 10, 5));
        result.CompletionDate.Should().Be(new DateOnly(2025, 2, 1));
        result.HasUnsafeOrdering.Should().BeTrue();
    }
}
