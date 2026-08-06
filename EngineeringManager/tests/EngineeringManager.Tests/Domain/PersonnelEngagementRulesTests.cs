using EngineeringManager.Domain.Personnel;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class PersonnelEngagementRulesTests
{
    [Fact]
    public void OverlappingPrimaryPeriodsAreRejected()
    {
        var periods = new[]
        {
            new EngagementPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), true),
            new EngagementPeriod(new DateOnly(2026, 3, 1), null, true)
        };

        var action = () => PersonnelEngagementRules.ValidatePrimaryPeriods(periods);

        action.Should().Throw<InvalidOperationException>().WithMessage("*重叠*");
    }

    [Fact]
    public void LatestEffectivePrimaryAffiliationIsCurrent()
    {
        var current = PersonnelEngagementRules.SelectCurrent(
            new[]
            {
                new CurrentEngagement(new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31), true, "旧项目"),
                new CurrentEngagement(new DateOnly(2026, 6, 1), null, true, "新项目")
            }, new DateOnly(2026, 8, 6));

        current.Should().NotBeNull();
        current!.ProjectName.Should().Be("新项目");
    }

    [Fact]
    public void FutureEngagementIsNotCurrentBeforeItsStartDate()
    {
        var current = PersonnelEngagementRules.SelectCurrent(
            [new CurrentEngagement(new DateOnly(2026, 9, 1), null, true, "未来项目")],
            new DateOnly(2026, 8, 6));

        current.Should().BeNull();
    }

    [Fact]
    public void DepartmentOwnerMustMatchSelectedOrganization()
    {
        var companyId = Guid.NewGuid();

        var action = () => PersonnelEngagementRules.ValidateDepartmentOwner(
            companyId,
            null,
            Guid.NewGuid(),
            null);

        action.Should().Throw<InvalidOperationException>().WithMessage("*不属于当前选择的组织*");
    }
}
