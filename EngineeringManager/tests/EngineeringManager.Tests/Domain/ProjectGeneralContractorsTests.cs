using EngineeringManager.Domain.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class ProjectGeneralContractorsTests
{
    [Fact]
    public void ParseAndSerializeSupportOneToThreeNames()
    {
        ProjectGeneralContractors.Parse(null).Should().BeEmpty();
        ProjectGeneralContractors.Parse("甲公司").Should().Equal("甲公司");
        ProjectGeneralContractors.Display("甲公司").Should().Be("甲公司");

        var encoded = ProjectGeneralContractors.Serialize(["甲公司", "乙公司", "丙公司", "丁公司"]);
        encoded.Should().NotBeNull();
        var parsed = ProjectGeneralContractors.Parse(encoded);
        parsed.Should().Equal("甲公司", "乙公司", "丙公司");
        ProjectGeneralContractors.Display(encoded).Should().Be("甲公司，乙公司，丙公司");
        ProjectGeneralContractors.Primary(encoded).Should().Be("甲公司");
    }

    [Fact]
    public void DisplayUsesShortNamesWhenFullTextIsTooLong()
    {
        var encoded = ProjectGeneralContractors.Serialize([
            "示范总包单位一号工程管理有限公司",
            "城市快速路桩基工程总承包单位有限公司"
        ]);

        var full = "示范总包单位一号工程管理有限公司，城市快速路桩基工程总承包单位有限公司";
        full.Length.Should().BeGreaterThan(20);

        var display = ProjectGeneralContractors.Display(encoded, maxLength: 24);
        display.Should().Contain("，");
        display.Should().NotContain("有限公司");
        display.Length.Should().BeLessThanOrEqualTo(24);
        display.Split("，", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Should().HaveCount(2);
    }
}
