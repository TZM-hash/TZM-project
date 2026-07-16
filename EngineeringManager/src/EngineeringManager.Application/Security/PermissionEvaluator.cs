using EngineeringManager.Domain.Security;

namespace EngineeringManager.Application.Security;

public static class PermissionEvaluator
{
    public static bool IsAllowed(
        IEnumerable<string> roleNames,
        IEnumerable<PermissionOverrideDto> overrides,
        string permissionKey)
    {
        if (!PermissionKeys.IsKnown(permissionKey))
        {
            return false;
        }

        var roles = roleNames.ToArray();
        if (roles.Contains(SystemRoles.SystemAdministrator, StringComparer.Ordinal))
        {
            return true;
        }

        var relevantOverrides = overrides
            .Where(item => string.Equals(item.PermissionKey, permissionKey, StringComparison.Ordinal))
            .ToArray();
        if (relevantOverrides.Any(item => item.Effect == PermissionEffect.Deny))
        {
            return false;
        }

        if (relevantOverrides.Any(item => item.Effect == PermissionEffect.Allow))
        {
            return true;
        }

        return roles.Any(role => PermissionKeys.DefaultsForRole(role).Contains(permissionKey));
    }
}
