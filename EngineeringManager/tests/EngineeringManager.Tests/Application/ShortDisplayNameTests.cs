using EngineeringManager.Application.Common;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class ShortDisplayNameTests
{
    [Fact]
    public void CopyTruncatesTheSourceBeforeAddingTheCompactSuffix()
    {
        ShortDisplayName.Copy("1234567890", 10).Should().Be("123456（副本）");
    }

    [Fact]
    public void CopyDoesNotAppendTheSuffixTwice()
    {
        ShortDisplayName.Copy("项目（副本）", 20).Should().Be("项目（副本）");
    }
}
