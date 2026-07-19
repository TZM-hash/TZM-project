using EngineeringManager.Domain.Security;
using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class CentralLedgerAuthorizationTests
{
    [Fact]
    public void LedgerPermissionsAreIndependentAndAssignedToConfirmedRoles()
    {
        var keys = new[]
        {
            PermissionKeys.ExternalLedgerRead,
            PermissionKeys.ExternalLedgerManage,
            PermissionKeys.InternalLedgerRead,
            PermissionKeys.InternalLedgerManage,
            PermissionKeys.FinanceYearsManage,
            PermissionKeys.FinanceReconciliationManage
        };
        keys.Should().OnlyContain(item => PermissionKeys.IsKnown(item));

        var finance = PermissionKeys.DefaultsForRole(SystemRoles.Finance);
        finance.Should().Contain(keys);
        var queryOnly = PermissionKeys.DefaultsForRole(SystemRoles.QueryOnly);
        queryOnly.Should().Contain(PermissionKeys.ExternalLedgerRead).And.Contain(PermissionKeys.InternalLedgerRead);
        queryOnly.Should().NotContain(PermissionKeys.ExternalLedgerManage)
            .And.NotContain(PermissionKeys.InternalLedgerManage)
            .And.NotContain(PermissionKeys.FinanceYearsManage)
            .And.NotContain(PermissionKeys.FinanceReconciliationManage);
        var projectManager = PermissionKeys.DefaultsForRole(SystemRoles.ProjectManager);
        projectManager.Should().NotContain(keys);
        PermissionKeys.DefaultsForRole(SystemRoles.ApplicationAdministrator).Should().Contain(keys);
        PermissionKeys.DefaultsForRole(SystemRoles.SystemAdministrator).Should().Contain(keys);
    }
}
