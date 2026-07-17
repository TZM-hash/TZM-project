using EngineeringManager.Application.Settings;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EngineeringManager.Tests.Application;

public sealed class SystemSettingsServiceTests
{
    [Fact]
    public async Task DefaultsMatchConfirmedGlobalDisplayProfile()
    {
        await using var fixture = await Fixture.CreateAsync();

        var settings = await fixture.Service.GetAsync(default);

        settings.Should().Be(new SystemDisplaySettings(
            VisualTheme.Default,
            MotionStyle.Technology,
            UiEffectsLevel.Medium,
            GlobalFont.SystemDefault,
            TableDensity.Standard));
    }

    [Fact]
    public async Task SystemAdministratorSavePersistsSettingsAndWritesBeforeAfterAudit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requested = new SystemDisplaySettings(
            VisualTheme.ClearGlass,
            MotionStyle.Apple,
            UiEffectsLevel.High,
            GlobalFont.MicrosoftYaHei,
            TableDensity.Compact);

        await fixture.Service.SaveAsync(new SettingsActor("sys", "系统管理员", true), requested, default);

        (await fixture.Service.GetAsync(default)).Should().Be(requested);
        (await fixture.Db.SystemSettings.CountAsync()).Should().Be(5);
        var audit = await fixture.Db.AuditLogs.SingleAsync(item => item.Action == "UpdateSystemDisplaySettings");
        audit.UserId.Should().Be("sys");
        audit.BeforeJson.Should().Contain("Medium");
        audit.AfterJson.Should().Contain("ClearGlass").And.Contain("High");
    }

    [Fact]
    public async Task ApplicationAdministratorCannotSaveGlobalSettings()
    {
        await using var fixture = await Fixture.CreateAsync();

        var action = () => fixture.Service.SaveAsync(
            new SettingsActor("app", "应用管理员", false),
            SystemDisplaySettings.Default,
            default);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        (await fixture.Db.SystemSettings.CountAsync()).Should().Be(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly MemoryCache cache;
        public ApplicationDbContext Db { get; }
        public SystemSettingsService Service { get; }

        private Fixture(SqliteConnection connection, MemoryCache cache, ApplicationDbContext db)
        {
            this.connection = connection;
            this.cache = cache;
            Db = db;
            Service = new SystemSettingsService(db, cache);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new ApplicationUser { Id = "sys", UserName = "sys", NormalizedUserName = "SYS", DisplayName = "系统管理员" });
            await db.SaveChangesAsync();
            return new Fixture(connection, new MemoryCache(new MemoryCacheOptions()), db);
        }

        public async ValueTask DisposeAsync()
        {
            cache.Dispose();
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
