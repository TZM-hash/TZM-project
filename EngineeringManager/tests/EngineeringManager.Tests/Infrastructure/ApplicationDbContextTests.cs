using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class ApplicationDbContextTests
{
    [Fact]
    public async Task IdentityModelCanCreateAndPersistAUserWithSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Users.Add(new ApplicationUser
        {
            UserName = "baseline-user",
            NormalizedUserName = "BASELINE-USER",
            Email = "baseline@example.test",
            NormalizedEmail = "BASELINE@EXAMPLE.TEST",
            DisplayName = "基线用户",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        (await db.Users.CountAsync()).Should().Be(1);
    }
}
