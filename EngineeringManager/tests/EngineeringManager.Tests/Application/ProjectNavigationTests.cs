using EngineeringManager.Application.Projects;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectNavigationTests
{
    [Fact]
    public void ResolveReturnsTheAdjacentProjectIdsAndDisablesAtBoundaries()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var orderedIds = new[] { first, second, third };

        var firstNavigation = ProjectNavigationResolver.Resolve(orderedIds, first);
        firstNavigation.PreviousProjectId.Should().BeNull();
        firstNavigation.NextProjectId.Should().Be(second);

        var middleNavigation = ProjectNavigationResolver.Resolve(orderedIds, second);
        middleNavigation.PreviousProjectId.Should().Be(first);
        middleNavigation.NextProjectId.Should().Be(third);

        var lastNavigation = ProjectNavigationResolver.Resolve(orderedIds, third);
        lastNavigation.PreviousProjectId.Should().Be(second);
        lastNavigation.NextProjectId.Should().BeNull();
    }

    [Fact]
    public void ResolveReturnsNoNavigationForAnUnknownProject()
    {
        var navigation = ProjectNavigationResolver.Resolve([Guid.NewGuid()], Guid.NewGuid());

        navigation.PreviousProjectId.Should().BeNull();
        navigation.NextProjectId.Should().BeNull();
    }
}
