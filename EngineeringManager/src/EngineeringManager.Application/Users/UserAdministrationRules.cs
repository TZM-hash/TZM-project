using EngineeringManager.Domain.Security;

namespace EngineeringManager.Application.Users;

public static class UserAdministrationRules
{
    public static void EnsureCanAssignRoles(
        bool callerIsSystemAdministrator,
        IReadOnlyCollection<string> roles)
    {
        if (!callerIsSystemAdministrator && roles.Contains(SystemRoles.SystemAdministrator, StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException("只有系统级管理员可以任命系统级管理员。");
        }

        var unknownRole = roles.FirstOrDefault(role => !SystemRoles.All.Contains(role, StringComparer.Ordinal));
        if (unknownRole is not null)
        {
            throw new ArgumentException($"未知角色：{unknownRole}", nameof(roles));
        }
    }
}
