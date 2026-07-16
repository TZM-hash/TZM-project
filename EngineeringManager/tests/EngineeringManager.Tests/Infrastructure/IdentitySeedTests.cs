using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Identity;
using EngineeringManager.Application.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class IdentitySeedTests
{
    [Fact]
    public async Task EnsureRolesCreatesAllTemplatesIdempotently()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();

        await IdentitySeed.EnsureRolesAsync(provider);
        await IdentitySeed.EnsureRolesAsync(provider);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in SystemRoles.All)
        {
            (await roleManager.RoleExistsAsync(roleName)).Should().BeTrue();
        }

        (await roleManager.Roles.CountAsync()).Should().Be(SystemRoles.All.Count);
    }

    [Fact]
    public void ApplicationAdministratorCannotAssignSystemAdministratorRole()
    {
        var action = () => UserAdministrationRules.EnsureCanAssignRoles(
            callerIsSystemAdministrator: false,
            [SystemRoles.SystemAdministrator]);

        action.Should().Throw<UnauthorizedAccessException>();
    }
}
