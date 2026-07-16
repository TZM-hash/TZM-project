using EngineeringManager.Domain.Security;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void SystemAdministratorUsesStableGlobalRoleName()
    {
        SystemRoles.SystemAdministrator.Should().Be("SystemAdministrator");
    }

    [Fact]
    public void ApplicationAdministratorHasDailyAdministrationButNotSystemSecurityPermission()
    {
        var permissions = PermissionKeys.DefaultsForRole(SystemRoles.ApplicationAdministrator);

        permissions.Should().Contain(PermissionKeys.UsersManage);
        permissions.Should().Contain(PermissionKeys.OrganizationManage);
        permissions.Should().NotContain(PermissionKeys.SystemSecurityManage);
        permissions.Should().NotContain(PermissionKeys.BackupRestore);
    }

    [Fact]
    public void UnknownPermissionKeysAreNotAcceptedByTheCatalog()
    {
        PermissionKeys.IsKnown("unknown.permission").Should().BeFalse();
        PermissionKeys.IsKnown(PermissionKeys.OrganizationManage).Should().BeTrue();
    }
}
