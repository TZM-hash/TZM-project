using EngineeringManager.Application.Employees;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class EmployeeNavigationTests
{
    [Fact]
    public void ResolveReturnsAdjacentEmployeeIdsAndDisablesAtBoundaries()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var orderedIds = new[] { first, second, third };

        var firstNavigation = EmployeeNavigationResolver.Resolve(orderedIds, first);
        firstNavigation.PreviousEmployeeId.Should().BeNull();
        firstNavigation.NextEmployeeId.Should().Be(second);

        var middleNavigation = EmployeeNavigationResolver.Resolve(orderedIds, second);
        middleNavigation.PreviousEmployeeId.Should().Be(first);
        middleNavigation.NextEmployeeId.Should().Be(third);

        var lastNavigation = EmployeeNavigationResolver.Resolve(orderedIds, third);
        lastNavigation.PreviousEmployeeId.Should().Be(second);
        lastNavigation.NextEmployeeId.Should().BeNull();
    }

    [Fact]
    public void ResolveReturnsNoNavigationForAnUnknownEmployee()
    {
        var navigation = EmployeeNavigationResolver.Resolve([Guid.NewGuid()], Guid.NewGuid());

        navigation.PreviousEmployeeId.Should().BeNull();
        navigation.NextEmployeeId.Should().BeNull();
    }
}
