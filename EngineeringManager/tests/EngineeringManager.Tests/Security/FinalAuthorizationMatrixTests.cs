using EngineeringManager.Domain.Security;
using FluentAssertions;

namespace EngineeringManager.Tests.Security;

public sealed class FinalAuthorizationMatrixTests
{
    [Fact]
    public void FinalRoleMatrixKeepsSystemApplicationFinanceSiteAndEquipmentBoundaries()
    {
        PermissionKeys.DefaultsForRole(SystemRoles.SystemAdministrator).Should().Contain(PermissionKeys.SystemSecurityManage);
        PermissionKeys.DefaultsForRole(SystemRoles.ApplicationAdministrator).Should().NotContain(PermissionKeys.SystemSecurityManage);
        PermissionKeys.DefaultsForRole(SystemRoles.Finance).Should().Contain(PermissionKeys.FinanceManage);
        PermissionKeys.DefaultsForRole(SystemRoles.SiteStaff).Should().NotContain(PermissionKeys.FinanceRead);
        PermissionKeys.DefaultsForRole(SystemRoles.EquipmentManager).Should().Contain(PermissionKeys.EquipmentUsageManage).And.NotContain(PermissionKeys.FinanceManage);
    }
}
