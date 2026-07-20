using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Web.Presentation;
using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectRelatedPartyBuilderTests
{
    [Fact]
    public void BuildCombinesGeneralContractorCrewsAndActivePartnersWithoutDuplicates()
    {
        var crewId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var partners = new[]
        {
            Partner(sharedId, "同名单位", BusinessPartnerRoleType.MaterialSupplier, true, "合作方备注"),
            Partner(crewId, "一队", BusinessPartnerRoleType.ConstructionCrew, true, null),
            Partner(Guid.NewGuid(), "无效合作方", BusinessPartnerRoleType.MiscellaneousSupplier, false, "不应显示")
        };
        var construction = new[]
        {
            CrewRecord(crewId, "一队"),
            CrewRecord(crewId, "一队"),
            CrewRecord(sharedId, "同名单位")
        };

        var result = ProjectRelatedPartyBuilder.Build("示范总包", partners, construction);

        result.Select(item => item.Name).Should().Equal("示范总包", "一队", "同名单位");
        result.Should().NotContain(item => item.Name == "无效合作方");
        result.Single(item => item.Name == "一队").Roles.Should().Equal("施工班组");
        result.Single(item => item.Name == "同名单位").Roles.Should().Equal("施工班组", "材料供应商");
        result.Single(item => item.Name == "同名单位").Notes.Should().Be("合作方备注");
    }

    [Fact]
    public void BuildDeduplicatesNamesIgnoringWhitespaceAndCase()
    {
        var result = ProjectRelatedPartyBuilder.Build(
            " ACME ",
            [Partner(Guid.NewGuid(), "acme", BusinessPartnerRoleType.CustomerOrGeneralContractor, true, "主要单位")],
            []);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("ACME");
        result[0].Roles.Should().Equal("总包", "甲方/总包");
        result[0].Notes.Should().Be("主要单位");
    }

    private static ProjectPartnerLinkDto Partner(Guid id, string name, BusinessPartnerRoleType role, bool active, string? notes) =>
        new(Guid.NewGuid(), id, name, role, null, null, false, active, notes);

    private static ProjectConstructionRecordDto CrewRecord(Guid id, string name) =>
        new(Guid.NewGuid(), Guid.NewGuid(), ProjectConstructionRecordType.ConstructionCrew, id, name,
            null, null, new DateOnly(2026, 7, 1), null, 20, 0, 20, null, null, null, false, Guid.NewGuid());
}
