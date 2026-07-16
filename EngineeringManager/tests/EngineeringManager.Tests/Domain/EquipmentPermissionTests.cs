using EngineeringManager.Domain.Security;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class EquipmentPermissionTests
{
    [Fact]
    public void EquipmentManagerHasOperationalPermissionsButNotFinanceAdministration()
    {
        SystemRoles.All.Should().Contain(SystemRoles.EquipmentManager);
        var permissions = PermissionKeys.DefaultsForRole(SystemRoles.EquipmentManager);
        permissions.Should().Contain(PermissionKeys.EquipmentRead);
        permissions.Should().Contain(PermissionKeys.EquipmentManage);
        permissions.Should().Contain(PermissionKeys.EquipmentUsageManage);
        permissions.Should().Contain(PermissionKeys.EquipmentMaintenanceManage);
        permissions.Should().NotContain(PermissionKeys.FinanceManage);
    }
}
