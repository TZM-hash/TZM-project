using EngineeringManager.Domain.Projects;
using EngineeringManager.Web.Presentation;
using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectRelatedPartyBuilderTests
{
    [Fact]
    public void BuildExpandsSerializedGeneralContractorsIntoSeparateParties()
    {
        var encoded = ProjectGeneralContractors.Serialize(["示范总包单位1有限公司", "水电施工有限公司"]);
        var parties = ProjectRelatedPartyBuilder.Build(encoded, [], []);

        parties.Should().HaveCount(2);
        parties.Select(item => item.Name).Should().BeEquivalentTo("示范总包单位1有限公司", "水电施工有限公司");
        parties.SelectMany(item => item.Roles).Should().OnlyContain(role => role == "总包");
        parties.Select(item => item.Name).Should().NotContain(name => name.Contains('['));
    }
}
