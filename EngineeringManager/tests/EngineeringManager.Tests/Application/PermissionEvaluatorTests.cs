using EngineeringManager.Application.Security;
using EngineeringManager.Domain.Security;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class PermissionEvaluatorTests
{
    [Fact]
    public void SystemAdministratorHasEveryKnownPermission()
    {
        PermissionEvaluator.IsAllowed(
            [SystemRoles.SystemAdministrator],
            [],
            PermissionKeys.SystemSecurityManage).Should().BeTrue();
    }

    [Fact]
    public void ExplicitDenyOverridesApplicationAdministratorDefaults()
    {
        PermissionEvaluator.IsAllowed(
            [SystemRoles.ApplicationAdministrator],
            [new PermissionOverrideDto(PermissionKeys.OrganizationManage, PermissionEffect.Deny)],
            PermissionKeys.OrganizationManage).Should().BeFalse();
    }

    [Fact]
    public void ExplicitAllowCanGrantAKnownPermissionToARegularRole()
    {
        PermissionEvaluator.IsAllowed(
            [SystemRoles.SiteStaff],
            [new PermissionOverrideDto(PermissionKeys.AuditRead, PermissionEffect.Allow)],
            PermissionKeys.AuditRead).Should().BeTrue();
    }

    [Fact]
    public void ApplicationAdministratorCannotManageSystemSecurityOrRestoreBackups()
    {
        PermissionEvaluator.IsAllowed(
            [SystemRoles.ApplicationAdministrator],
            [],
            PermissionKeys.SystemSecurityManage).Should().BeFalse();
        PermissionEvaluator.IsAllowed(
            [SystemRoles.ApplicationAdministrator],
            [],
            PermissionKeys.BackupRestore).Should().BeFalse();
    }

    [Fact]
    public void UnknownPermissionsAreAlwaysDenied()
    {
        PermissionEvaluator.IsAllowed(
            [SystemRoles.SystemAdministrator],
            [],
            "unknown.permission").Should().BeFalse();
    }
}
