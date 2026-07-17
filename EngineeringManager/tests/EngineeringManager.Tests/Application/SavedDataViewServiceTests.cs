using System.Text.Json;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.Settings;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataViews;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class SavedDataViewServiceTests
{
    private static readonly DataViewDefinition Definition = new(
        "projects",
        new HashSet<string>(["Keyword", "Stage", "CompanyId"], StringComparer.Ordinal),
        new HashSet<string>(["ProjectNumber", "Name", "Stage", "CurrentAmount"], StringComparer.Ordinal),
        new HashSet<string>(["ProjectNumber", "CurrentAmount"], StringComparer.Ordinal));

    [Fact]
    public async Task DefaultViewIsUniquePerUserAndPage()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.SaveAsync("u1", new SaveDataViewRequest(null, "projects", "施工中", true, "{\"Stage\":\"UnderConstruction\"}", "[\"ProjectNumber\",\"Name\"]", "ProjectNumber", false, TableDensity.Compact, 50), Definition, default);
        await fixture.Service.SaveAsync("u1", new SaveDataViewRequest(null, "projects", "未收款", true, "{\"Keyword\":\"未收款\"}", "[\"ProjectNumber\",\"CurrentAmount\"]", "CurrentAmount", true, TableDensity.Standard, 20), Definition, default);

        var views = await fixture.Service.ListAsync("u1", Definition, default);

        views.Should().HaveCount(2).And.ContainSingle(item => item.IsDefault);
        views.Single(item => item.IsDefault).Name.Should().Be("未收款");
    }

    [Fact]
    public async Task ViewsAreIsolatedByUser()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.SaveAsync("u1", new SaveDataViewRequest(null, "projects", "用户一", false, "{}", "[]", null, false, TableDensity.Standard, 20), Definition, default);
        await fixture.Service.SaveAsync("u2", new SaveDataViewRequest(null, "projects", "用户二", false, "{}", "[]", null, false, TableDensity.Standard, 20), Definition, default);

        (await fixture.Service.ListAsync("u1", Definition, default)).Should().ContainSingle(item => item.Name == "用户一");
        (await fixture.Service.ListAsync("u2", Definition, default)).Should().ContainSingle(item => item.Name == "用户二");
    }

    [Fact]
    public async Task InvalidOrRemovedFieldsAreDiscarded()
    {
        await using var fixture = await Fixture.CreateAsync();
        var saved = await fixture.Service.SaveAsync("u1", new SaveDataViewRequest(null, "projects", "升级兼容", false, "{\"Keyword\":\"市政\",\"Removed\":\"x\"}", "[\"ProjectNumber\",\"RemovedColumn\"]", "RemovedSort", false, TableDensity.Standard, 100), Definition, default);

        using var filters = JsonDocument.Parse(saved.FilterJson);
        filters.RootElement.TryGetProperty("Keyword", out _).Should().BeTrue();
        filters.RootElement.TryGetProperty("Removed", out _).Should().BeFalse();
        saved.ColumnJson.Should().Be("[\"ProjectNumber\"]");
        saved.SortKey.Should().BeNull();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public SavedDataViewService Service { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext db)
        {
            this.connection = connection;
            Db = db;
            Service = new SavedDataViewService(db);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(
                new ApplicationUser { Id = "u1", UserName = "u1", NormalizedUserName = "U1", DisplayName = "用户一" },
                new ApplicationUser { Id = "u2", UserName = "u2", NormalizedUserName = "U2", DisplayName = "用户二" });
            await db.SaveChangesAsync();
            return new Fixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
