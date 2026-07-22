using EngineeringManager.Infrastructure.Development;
using EngineeringManager.Domain.Employees;
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
    public async Task SeederRecreatesRequiredCompanyCategoriesAfterBusinessDataReset()
    {
        await using var fixture = await SampleSeederFixture.CreateAsync();
        await fixture.Db.CompanyCategories.ExecuteDeleteAsync();

        await fixture.SeedAsync();

        (await fixture.Db.CompanyCategories.CountAsync()).Should().BeGreaterThanOrEqualTo(4);
        (await fixture.Db.LegalEntities.CountAsync()).Should().Be(SampleDataCatalog.CompanyCount);
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
        (await fixture.Db.PayrollBatches.CountAsync()).Should().Be(13);
        (await fixture.Db.PayrollBatches.CountAsync(item => item.IsUnifiedDisbursement)).Should().Be(1);
        (await fixture.Db.ConstructionWorkers.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
        var temporaryEmployee = await fixture.Db.Employees.SingleAsync(item => item.EmployeeType == EmployeeType.Temporary);
        var unified = await fixture.Db.PayrollBatches.Include(item => item.Payments).SingleAsync(item => item.IsUnifiedDisbursement);
        unified.Payments.Should().ContainSingle(item =>
            item.RecipientType == PayrollRecipientType.Employee &&
            item.EmployeeId == temporaryEmployee.Id);
        unified.Payments.Sum(item => item.Amount).Should().Be(unified.ActualAmount);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.SourceType == EngineeringManager.Domain.Finance.AccountTransactionSourceType.PayrollPayment && item.SourceId == unified.Id)).Should().Be(1);
        (await fixture.Db.StageResults.CountAsync()).Should().BeGreaterThan(10);
        (await fixture.Db.ReminderItems.CountAsync()).Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task SeederCreatesAnnualLedgerAndNotesScenarioWithoutDuplicates()
    {
        await using var fixture = await SampleSeederFixture.CreateAsync();

        await fixture.SeedCompleteAsync();
        await fixture.SeedCompleteAsync();

        (await fixture.Db.BusinessYears.CountAsync()).Should().Be(1);
        (await fixture.Db.EmployeeWageEntries.CountAsync()).Should().BeGreaterThanOrEqualTo(3);
        (await fixture.Db.EmployeeReceipts.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
        (await fixture.Db.EmployeeFinancialAdjustments.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
        (await fixture.Db.EmployeeFinancialAdjustments.CountAsync(item => item.ReversalOfId != null)).Should().BeGreaterThanOrEqualTo(1);
        (await fixture.Db.Employees.CountAsync(item => item.Notes != null && item.Notes != string.Empty)).Should().Be(await fixture.Db.Employees.CountAsync());
        (await fixture.Db.Projects.CountAsync(item => item.Notes != null && item.Notes != string.Empty)).Should().Be(SampleDataCatalog.ProjectCount);
    }

    [Fact]
    public void ResetScriptRequiresDevelopmentAndTestSuffix()
    {
        var script = ReadRepositoryFile("scripts", "reset-test-database.ps1");

        script.Should().Contain("$ErrorActionPreference = 'Stop'");
        script.Should().Contain("-notmatch '_Test$'");
        script.Should().Contain("set_ConnectionString");
        script.Should().NotContain("$connectionBuilder.ConnectionString =");
        script.Should().Contain(".tools\\dotnet-tools");
        script.Should().Contain("[IO.Path]::PathSeparator");
        script.Should().Contain("$env:DOTNET_ROOT");
        script.Should().Contain(".dotnet");
        script.Should().Contain("$env:ASPNETCORE_ENVIRONMENT = 'Development'");
        script.Should().Contain("DevelopmentSampleData__Enabled");
        script.Should().Contain("-AllowOfficialDataDeletion");
        script.Should().Contain("OFFICIAL-%");
        script.Should().Contain("检测到正式自有公司资料");
        script.Should().NotContain("EngineeringManager_Production");
    }

    private static string ReadRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln")))
        {
            directory = directory.Parent;
        }
        var root = directory ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
        return File.ReadAllText(Path.Combine(new[] { root.FullName }.Concat(parts).ToArray()));
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
