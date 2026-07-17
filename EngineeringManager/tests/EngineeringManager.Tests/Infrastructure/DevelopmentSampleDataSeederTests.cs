using EngineeringManager.Infrastructure.Development;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class DevelopmentSampleDataSeederTests
{
    [Fact]
    public void CatalogUsesConfirmedMediumScenario()
    {
        SampleDataCatalog.CompanyCount.Should().Be(5);
        SampleDataCatalog.ProjectCount.Should().Be(15);
        SampleDataCatalog.EmployeeCount.Should().Be(30);
        SampleDataCatalog.PartnerCount.Should().Be(12);
        SampleDataCatalog.EquipmentCount.Should().Be(15);
    }

    [Theory]
    [InlineData("Production", "EngineeringManager_Test")]
    [InlineData("Development", "EngineeringManager")]
    [InlineData("Development", "ProductionDb")]
    public void SafetyGuardRejectsNonDevelopmentOrNonTestDatabase(string environment, string database)
    {
        var action = () => DevelopmentSampleDataSeeder.ValidateSafety(environment, database);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SafetyGuardAllowsExplicitDevelopmentTestDatabase()
    {
        var action = () => DevelopmentSampleDataSeeder.ValidateSafety("Development", "EngineeringManager_Test");
        action.Should().NotThrow();
    }

    [Fact]
    public void GeneratedPasswordIsEasyToTypeAndMeetsIdentityRules()
    {
        var first = DevelopmentSampleDataSeeder.GenerateTestPassword();
        var second = DevelopmentSampleDataSeeder.GenerateTestPassword();
        first.Should().StartWith("TestAdmin").And.HaveLength(13);
        first.Should().MatchRegex("^[A-Za-z0-9]+$");
        first.Should().NotBe(second);
    }

    [Fact]
    public async Task SeederCreatesConfirmedCoreScenarioWithoutDuplicates()
    {
        await using var fixture = await SampleSeederFixture.CreateAsync();

        await fixture.SeedAsync();
        await fixture.SeedAsync();

        (await fixture.Db.LegalEntities.CountAsync()).Should().Be(SampleDataCatalog.CompanyCount);
        (await fixture.Db.Projects.CountAsync()).Should().Be(SampleDataCatalog.ProjectCount);
        (await fixture.Db.Contracts.CountAsync()).Should().BeInRange(18, 20);
        (await fixture.Db.Employees.CountAsync()).Should().Be(SampleDataCatalog.EmployeeCount);
        foreach (var role in new[]
                 {
                     SystemRoles.SystemAdministrator,
                     SystemRoles.ApplicationAdministrator,
                     SystemRoles.Finance,
                     SystemRoles.ProjectManager,
                     SystemRoles.SiteStaff
                 })
        {
            (await fixture.UserManager.GetUsersInRoleAsync(role)).Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task SeederCreatesTwelveMonthBalancedBusinessScenario()
    {
        await using var fixture = await SampleSeederFixture.CreateAsync();

        await fixture.SeedCompleteAsync();
        await fixture.SeedCompleteAsync();

        (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(SampleDataCatalog.PartnerCount);
        (await fixture.Db.Equipment.CountAsync()).Should().Be(SampleDataCatalog.EquipmentCount);
        (await fixture.Db.ReceivableEntries.CountAsync()).Should().BeGreaterThan(20);
        (await fixture.Db.CollectionEntries.CountAsync()).Should().BeGreaterThan(15);
        (await fixture.Db.InvoiceEntries.CountAsync()).Should().BeGreaterThan(15);
        (await fixture.Db.PayrollBatches.CountAsync()).Should().Be(12);
        (await fixture.Db.StageResults.CountAsync()).Should().BeGreaterThan(10);
        (await fixture.Db.ReminderItems.CountAsync()).Should().BeGreaterThan(5);
    }

    private sealed class SampleSeederFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ServiceProvider provider;
        private readonly string contentRoot;

        public ApplicationDbContext Db { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        private SampleDataBuilder Builder { get; }

        private SampleSeederFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            string contentRoot,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            this.connection = connection;
            this.provider = provider;
            this.contentRoot = contentRoot;
            Db = db;
            UserManager = userManager;
            Builder = new SampleDataBuilder(db, userManager, TimeProvider.System);
        }

        public static async Task<SampleSeederFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=EngineeringManager_Test;Mode=Memory;Cache=Shared");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddDebug());
            services.AddDataProtection();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            await IdentitySeed.EnsureRolesAsync(provider);
            var contentRoot = Path.Combine(Path.GetTempPath(), "EngineeringManager.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRoot);
            return new SampleSeederFixture(connection, provider, contentRoot, db, provider.GetRequiredService<UserManager<ApplicationUser>>());
        }

        public async Task SeedAsync()
        {
            await Builder.BuildCoreAsync(CancellationToken.None);
            await Db.SaveChangesAsync();
        }

        public async Task SeedCompleteAsync()
        {
            await Builder.BuildCompleteAsync(contentRoot, CancellationToken.None);
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
        }
    }
}
