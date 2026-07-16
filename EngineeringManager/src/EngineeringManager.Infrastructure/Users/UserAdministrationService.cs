using EngineeringManager.Application.Users;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Users;

public sealed class UserAdministrationService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : IUserAdministrationService
{
    public async Task<IReadOnlyList<UserAdminDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().OrderBy(user => user.DisplayName).ToListAsync(cancellationToken);
        var memberships = await db.UserOrganizationMemberships
            .AsNoTracking()
            .Where(item => item.IsPrimary && item.IsActive)
            .Select(item => new { item.UserId, item.OrganizationUnit.Name })
            .ToListAsync(cancellationToken);
        var legalEntities = await db.UserLegalEntityAccesses
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.UserId, item.LegalEntity.ShortName })
            .ToListAsync(cancellationToken);

        var result = new List<UserAdminDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserAdminDto(
                user.Id,
                user.UserName ?? string.Empty,
                user.DisplayName,
                user.IsEnabled,
                roles.Order(StringComparer.Ordinal).ToArray(),
                memberships.FirstOrDefault(item => item.UserId == user.Id)?.Name,
                legalEntities.Where(item => item.UserId == user.Id).Select(item => item.ShortName).Order(StringComparer.Ordinal).ToArray()));
        }

        return result;
    }

    public async Task SetEnabledAsync(
        string userId,
        bool isEnabled,
        bool callerIsSystemAdministrator,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("用户不存在。");
        var roles = await userManager.GetRolesAsync(user);
        if (!callerIsSystemAdministrator && roles.Contains(SystemRoles.SystemAdministrator, StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException("应用级管理员不能停用系统级管理员。");
        }

        cancellationToken.ThrowIfCancellationRequested();
        user.IsEnabled = isEnabled;
        var result = await userManager.UpdateAsync(user);
        EnsureSucceeded(result, "更新用户状态");
    }

    public async Task SetRolesAsync(
        string userId,
        IReadOnlyCollection<string> roles,
        bool callerIsSystemAdministrator,
        CancellationToken cancellationToken)
    {
        UserAdministrationRules.EnsureCanAssignRoles(callerIsSystemAdministrator, roles);
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("用户不存在。");
        var currentRoles = await userManager.GetRolesAsync(user);
        cancellationToken.ThrowIfCancellationRequested();

        var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles.Except(roles, StringComparer.Ordinal));
        EnsureSucceeded(removeResult, "移除用户角色");
        var addResult = await userManager.AddToRolesAsync(user, roles.Except(currentRoles, StringComparer.Ordinal));
        EnsureSucceeded(addResult, "添加用户角色");
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{action}失败：{string.Join("；", result.Errors.Select(error => error.Description))}");
        }
    }
}
