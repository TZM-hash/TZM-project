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

    [Fact]
    public void CompanyPermissionsAreAvailableToExpectedRoles()
    {
        PermissionKeys.IsKnown(PermissionKeys.CompaniesRead).Should().BeTrue();
        PermissionKeys.IsKnown(PermissionKeys.EmployeeCertificatesManage).Should().BeTrue();
        PermissionKeys.IsKnown(PermissionKeys.CompaniesManage).Should().BeTrue();
        PermissionKeys.DefaultsForRole(SystemRoles.ApplicationAdministrator).Should().Contain(PermissionKeys.CompaniesManage);
        PermissionKeys.DefaultsForRole(SystemRoles.Finance).Should().Contain(PermissionKeys.CompaniesRead);
        PermissionKeys.DefaultsForRole(SystemRoles.QueryOnly).Should().Contain(PermissionKeys.CompaniesRead);
    }
}
