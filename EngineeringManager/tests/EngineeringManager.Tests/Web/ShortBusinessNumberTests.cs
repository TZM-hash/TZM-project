using EngineeringManager.Web.Presentation;
using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ShortBusinessNumberTests
{
    [Fact]
    public void NextUsesFirstAvailableShortNumberWithinPrefix()
    {
        ShortBusinessNumber.Next(["XM0001", "XM0003", "OLD-XM-2"], "XM").Should().Be("XM0002");
        ShortBusinessNumber.Next(["YG0001", "yg0002"], "YG").Should().Be("YG0003");
        ShortBusinessNumber.Next([], "HZ").Should().Be("HZ0001");
    }
}
