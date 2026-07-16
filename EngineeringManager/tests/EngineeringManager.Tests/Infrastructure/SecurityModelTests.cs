using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class SecurityModelTests
{
    [Fact]
    public async Task PermissionScopeAndAuditLogCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser { UserName = "auditor", NormalizedUserName = "AUDITOR", DisplayName = "审计员" };
        db.Users.Add(user);
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            User = user,
            PermissionKey = PermissionKeys.AuditRead,
            Effect = PermissionEffect.Allow,
            Reason = "负责权限审计"
        });
        db.UserDataScopes.Add(new UserDataScope
        {
            User = user,
            ScopeType = PermissionScopeType.AssignedProjects
        });
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            UserName = user.DisplayName,
            Action = "PermissionChanged",
            EntityType = "ApplicationUser",
            EntityId = user.Id,
            Reason = "调整审计权限",
            BeforeJson = "{}",
            AfterJson = "{\"audit.read\":true}",
            IpAddress = "127.0.0.1",
            RequestId = "request-1"
        });

        await db.SaveChangesAsync();

        (await db.UserPermissionOverrides.SingleAsync()).Reason.Should().Be("负责权限审计");
        (await db.UserDataScopes.SingleAsync()).ScopeType.Should().Be(PermissionScopeType.AssignedProjects);
        (await db.AuditLogs.SingleAsync()).AfterJson.Should().Contain("audit.read");
    }
}
