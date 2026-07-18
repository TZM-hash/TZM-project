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

    [Fact]
    public void CrewAndSensitivePayrollPermissionsRemainWhileTemporaryWorkerPermissionsAreRemoved()
    {
        PermissionKeys.IsKnown(PermissionKeys.ConstructionCrewsRead).Should().BeTrue();
        PermissionKeys.IsKnown(PermissionKeys.ConstructionCrewsManage).Should().BeTrue();
        PermissionKeys.IsKnown("temporary-workers.read").Should().BeFalse();
        PermissionKeys.IsKnown("temporary-workers.manage").Should().BeFalse();
        typeof(PermissionKeys).GetFields().Select(field => field.Name).Should().NotContain([
            "TemporaryWorkersRead",
            "TemporaryWorkersManage"
        ]);
        PermissionKeys.IsKnown(PermissionKeys.SensitivePersonnelRead).Should().BeTrue();
        PermissionKeys.DefaultsForRole(SystemRoles.ApplicationAdministrator).Should().Contain(PermissionKeys.ConstructionCrewsManage);
        PermissionKeys.DefaultsForRole(SystemRoles.QueryOnly).Should().Contain(PermissionKeys.ConstructionCrewsRead);
        foreach (var role in SystemRoles.All)
        {
            PermissionKeys.DefaultsForRole(role).Should().NotContain("temporary-workers.read");
            PermissionKeys.DefaultsForRole(role).Should().NotContain("temporary-workers.manage");
        }
    }
}
