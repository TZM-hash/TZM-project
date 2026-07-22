using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.EmployeeAnnualLedger;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class EmployeeAnnualLedgerServiceTests
{
    [Fact]
    public async Task DeductionWagePreservesCustomUnitAndStoresNegativeFinalAmount()
    {
        await using var fixture = await LedgerFixture.CreateAsync();

        var entry = await fixture.Service.AddWageEntryAsync(
            new CreateEmployeeWageEntryRequest(
                fixture.Employee.Id,
                fixture.CurrentYear.Id,
                new DateOnly(2026, 7, 1),
                new DateOnly(2026, 7, 2),
                EmployeeWageCategory.OtherWage,
                EmployeeWageCalculationMethod.CustomUnit,
                PayrollItemNature.Deduction,
                2m,
                "请假天",
                150m,
                null,
                null,
                null,
                null,
                20m,
                "请假扣款"),
            CancellationToken.None);

        entry.Unit.Should().Be("请假天");
        entry.AutomaticAmount.Should().Be(-300m);
        entry.FinalAmount.Should().Be(-280m);
        entry.IsUnassignedMigrantWage.Should().BeFalse();
    }

    [Fact]
    public async Task MigrantWageRequiresLaborPartnerButAllowsNoProject()
    {
        await using var fixture = await LedgerFixture.CreateAsync();

        var missingLabor = () => fixture.Service.AddWageEntryAsync(
            new CreateEmployeeWageEntryRequest(
                fixture.Employee.Id,
                fixture.CurrentYear.Id,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31),
                EmployeeWageCategory.MigrantWorkerWage,
                EmployeeWageCalculationMethod.FixedAmount,
                PayrollItemNature.Earning,
                null,
                null,
                null,
                4_000m,
                null,
                null,
                null,
                0m,
                null),
            CancellationToken.None);

        await missingLabor.Should().ThrowAsync<ArgumentException>().WithMessage("*劳务公司*");

        var entry = await fixture.Service.AddWageEntryAsync(
            new CreateEmployeeWageEntryRequest(
                fixture.Employee.Id,
                fixture.CurrentYear.Id,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31),
                EmployeeWageCategory.MigrantWorkerWage,
                EmployeeWageCalculationMethod.FixedAmount,
                PayrollItemNature.Earning,
                null,
                null,
                null,
                4_000m,
                null,
                null,
                fixture.LaborPartner.Id,
                0m,
                null),
            CancellationToken.None);

        entry.ProjectId.Should().BeNull();
        entry.LaborBusinessPartnerId.Should().Be(fixture.LaborPartner.Id);
        entry.IsUnassignedMigrantWage.Should().BeFalse();
    }

    [Fact]
    public async Task WageEntryCannotCrossBusinessYearOrBeAddedToHistoricalYear()
    {
        await using var fixture = await LedgerFixture.CreateAsync();
        var crossYear = () => fixture.Service.AddWageEntryAsync(
            WageRequest(fixture.Employee.Id, fixture.CurrentYear.Id, new DateOnly(2027, 2, 20), new DateOnly(2027, 3, 2)),
            CancellationToken.None);
        var historical = () => fixture.Service.AddWageEntryAsync(
            WageRequest(fixture.Employee.Id, fixture.HistoricalYear.Id, new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 31)),
            CancellationToken.None);

        await crossYear.Should().ThrowAsync<InvalidOperationException>().WithMessage("*不能跨业务年度*");
        await historical.Should().ThrowAsync<InvalidOperationException>().WithMessage("*历史年度*");
    }

    [Fact]
    public async Task AdjustmentIsImmutableAndCanOnlyBeReversedByOppositeEntry()
    {
        await using var fixture = await LedgerFixture.CreateAsync();
        var adjustment = await fixture.Service.AddAdjustmentAsync(
            new CreateEmployeeFinancialAdjustmentRequest(
                fixture.Employee.Id,
                fixture.HistoricalYear.Id,
                new DateOnly(2025, 12, 31),
                500m,
                EmployeeFinancialAdjustmentType.AdministratorAdjustment,
                "补记旧账"),
            CancellationToken.None);

        var reversal = await fixture.Service.ReverseAdjustmentAsync(
            adjustment.Id,
            new DateOnly(2026, 7, 20),
            "冲销误记",
            CancellationToken.None);

        reversal.Amount.Should().Be(-500m);
        reversal.ReversalOfId.Should().Be(adjustment.Id);
        (await fixture.Db.EmployeeFinancialAdjustments.CountAsync()).Should().Be(2);
    }

    private static CreateEmployeeWageEntryRequest WageRequest(Guid employeeId, Guid businessYearId, DateOnly startDate, DateOnly endDate) =>
        new(
            employeeId,
            businessYearId,
            startDate,
            endDate,
            EmployeeWageCategory.SocialSecurityWage,
            EmployeeWageCalculationMethod.FixedAmount,
            PayrollItemNature.Earning,
            null,
            null,
            null,
            3_000m,
            null,
            null,
            null,
            0m,
            null);

    private sealed class LedgerFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private LedgerFixture(
            SqliteConnection connection,
            ApplicationDbContext db,
            EmployeeAnnualLedgerService service,
            Employee employee,
            BusinessYear historicalYear,
            BusinessYear currentYear,
            BusinessPartner laborPartner)
        {
            this.connection = connection;
            Db = db;
            Service = service;
            Employee = employee;
            HistoricalYear = historicalYear;
            CurrentYear = currentYear;
            LaborPartner = laborPartner;
        }

        public ApplicationDbContext Db { get; }
        public EmployeeAnnualLedgerService Service { get; }
        public Employee Employee { get; }
        public BusinessYear HistoricalYear { get; }
        public BusinessYear CurrentYear { get; }
        public BusinessPartner LaborPartner { get; }

        public static async Task<LedgerFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var employee = new Employee { EmployeeNumber = "ANNUAL-E", Name = "年度员工", EmployeeType = EmployeeType.Formal };
            var historicalYear = new BusinessYear { Name = "2025年度", StartDate = new DateOnly(2025, 3, 1), EndDate = new DateOnly(2026, 2, 28) };
            var currentYear = new BusinessYear { Name = "2026年度", StartDate = new DateOnly(2026, 3, 1), EndDate = new DateOnly(2027, 2, 28) };
            var laborPartner = new BusinessPartner { PartnerNumber = "LABOR-1", Name = "测试劳务公司", ShortName = "测试劳务" };
            laborPartner.Roles.Add(new BusinessPartnerRole { Partner = laborPartner, RoleType = BusinessPartnerRoleType.ConstructionCrew });
            db.AddRange(employee, historicalYear, currentYear, laborPartner);
            await db.SaveChangesAsync();
            var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.FromHours(8)));
            return new LedgerFixture(connection, db, new EmployeeAnnualLedgerService(db, timeProvider), employee, historicalYear, currentYear, laborPartner);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
