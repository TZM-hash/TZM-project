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
    [Theory]
    [InlineData(VisualTheme.Default, "theme-default")]
    [InlineData(VisualTheme.ClearGlass, "theme-clear-glass")]
    [InlineData(VisualTheme.LavenderCream, "theme-lavender-cream")]
    public void EveryVisualThemeMapsToItsExpectedCssClass(VisualTheme theme, string expectedCssClass)
    {
        var settings = SystemDisplaySettings.Default with { Theme = theme };

        settings.ThemeCssClass.Should().Be(expectedCssClass);
    }

    [Theory]
    [InlineData(VisualTheme.Default, "#2563eb")]
    [InlineData(VisualTheme.ClearGlass, "#2563eb")]
    [InlineData(VisualTheme.LavenderCream, "#7653d6")]
    public void EveryVisualThemeMapsToItsExpectedBrowserThemeColor(VisualTheme theme, string expectedColor)
    {
        var settings = SystemDisplaySettings.Default with { Theme = theme };

        settings.ThemeColor.Should().Be(expectedColor);
    }

    [Theory]
    [InlineData(UiAppearanceStyle.Classic, "appearance-classic")]
    [InlineData(UiAppearanceStyle.RoundedSoft, "appearance-rounded-soft")]
    public void EveryAppearanceStyleMapsToItsExpectedCssClass(
        UiAppearanceStyle appearance,
        string expectedCssClass)
    {
        var settings = SystemDisplaySettings.Default with { Appearance = appearance };

        settings.AppearanceCssClass.Should().Be(expectedCssClass);
    }

    [Fact]
    public void DefaultAppearanceStyleKeepsExistingClassicVisuals()
    {
        SystemDisplaySettings.Default.Appearance.Should().Be(UiAppearanceStyle.Classic);
        SystemDisplaySettings.Default.AppearanceCssClass.Should().Be("appearance-classic");
    }

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
            TableDensity.Standard,
            GlobalFontSize.Standard));
    }

    [Fact]
    public async Task DefaultGlobalFontSizeIsStandardAndCanBePersisted()
    {
        await using var fixture = await Fixture.CreateAsync();

        var settings = await fixture.Service.GetAsync(default);
        var property = settings.GetType().GetProperty("FontSize");

        property.Should().NotBeNull();
        property!.GetValue(settings)!.ToString().Should().Be("Standard");
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
            TableDensity.Compact,
            GlobalFontSize.Large);

        await fixture.Service.SaveAsync(new SettingsActor("sys", "系统管理员", true), requested, default);

        (await fixture.Service.GetAsync(default)).Should().Be(requested);
        (await fixture.Db.SystemSettings.CountAsync()).Should().Be(7);
        var audit = await fixture.Db.AuditLogs.SingleAsync(item => item.Action == "UpdateSystemDisplaySettings");
        audit.UserId.Should().Be("sys");
        audit.BeforeJson.Should().Contain("Medium");
        audit.AfterJson.Should().Contain("ClearGlass").And.Contain("High");
    }

    [Fact]
    public async Task LavenderCreamThemeMapsToCssClassAndPersistsInExistingSetting()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requested = SystemDisplaySettings.Default with { Theme = VisualTheme.LavenderCream };

        requested.ThemeCssClass.Should().Be("theme-lavender-cream");

        await fixture.Service.SaveAsync(new SettingsActor("sys", "系统管理员", true), requested, default);

        (await fixture.Service.GetAsync(default)).Theme.Should().Be(VisualTheme.LavenderCream);
        var stored = await fixture.Db.SystemSettings.SingleAsync(item => item.Key == "Display.Theme");
        stored.Value.Should().Be("LavenderCream");
    }

    [Fact]
    public async Task RoundedSoftAppearancePersistsInExistingSettingsTable()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requested = SystemDisplaySettings.Default with
        {
            Appearance = UiAppearanceStyle.RoundedSoft
        };

        await fixture.Service.SaveAsync(
            new SettingsActor("sys", "系统管理员", true),
            requested,
            default);

        (await fixture.Service.GetAsync(default)).Appearance
            .Should().Be(UiAppearanceStyle.RoundedSoft);
        var stored = await fixture.Db.SystemSettings
            .SingleAsync(item => item.Key == "Display.Appearance");
        stored.Value.Should().Be("RoundedSoft");
    }

    [Fact]
    public async Task MissingOrInvalidAppearanceSettingFallsBackToClassic()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.SystemSettings.Add(new SystemSetting
        {
            Key = "Display.Appearance",
            Value = "UnknownAppearance",
            UpdatedByUserId = "sys"
        });
        await fixture.Db.SaveChangesAsync();

        (await fixture.Service.GetAsync(default)).Appearance
            .Should().Be(UiAppearanceStyle.Classic);
    }

    [Fact]
    public async Task InvalidAppearanceIsRejectedBeforeAnySettingIsWritten()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invalid = SystemDisplaySettings.Default with
        {
            Appearance = (UiAppearanceStyle)999
        };

        var action = () => fixture.Service.SaveAsync(
            new SettingsActor("sys", "系统管理员", true),
            invalid,
            default);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await fixture.Db.SystemSettings.CountAsync()).Should().Be(0);
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

    [Fact]
    public async Task InvalidGlobalFontSizeIsRejectedBeforeAnySettingIsWritten()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invalid = SystemDisplaySettings.Default with { FontSize = (GlobalFontSize)999 };

        var action = () => fixture.Service.SaveAsync(
            new SettingsActor("sys", "系统管理员", true),
            invalid,
            default);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
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
