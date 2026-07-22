using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.EmployeeLedger;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.EmployeeAnnualLedger;
using EngineeringManager.Infrastructure.EmployeeLedger;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class EmployeePayableWorkspaceServiceTests
{
    [Fact]
    public async Task PenaltyUsesPositiveInputButStoresNegativeFinalAmountAndReturnsDimensions()
    {
        await using var fixture = await Fixture.CreateAsync();

        var created = await fixture.AnnualService.AddWageEntryAsync(
            new CreateEmployeeWageEntryRequest(
                fixture.Employee.Id,
                fixture.BusinessYear.Id,
                new DateOnly(2026, 7, 1),
                new DateOnly(2026, 7, 31),
                EmployeeWageCategory.SocialSecurityWage,
                EmployeeWageCalculationMethod.FixedAmount,
                PayrollItemNature.Earning,
                null,
                null,
                null,
                300m,
                fixture.LegalEntity.Id,
                fixture.Project.Id,
                null,
                0m,
                "迟到罚款",
                EmployeeWageEntryType.Penalty),
            CancellationToken.None);

        created.EntryType.Should().Be(EmployeeWageEntryType.Penalty);
        created.Nature.Should().Be(PayrollItemNature.Deduction);
        created.FinalAmount.Should().Be(-300m);

        var rows = await fixture.AnnualService.GetWageEntriesAsync(fixture.Employee.Id, fixture.BusinessYear.Id, CancellationToken.None);
        rows.Should().ContainSingle().Which.Should().Match<EmployeeWageEntryDto>(row =>
            row.EntryType == EmployeeWageEntryType.Penalty &&
            row.LegalEntityName == fixture.LegalEntity.Name &&
            row.ProjectName == fixture.Project.Name);
    }

    [Fact]
    public async Task SystemGeneratedPersonalAdvancePayableRejectsDirectEditing()
    {
        await using var fixture = await Fixture.CreateAsync();
        var entry = new EmployeeWageEntry
        {
            Employee = fixture.Employee,
            BusinessYear = fixture.BusinessYear,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 1),
            EntryType = EmployeeWageEntryType.Other,
            WageCategory = EmployeeWageCategory.SocialSecurityWage,
            CalculationMethod = EmployeeWageCalculationMethod.FixedAmount,
            Nature = PayrollItemNature.Earning,
            AutomaticAmount = 1_000m,
            FinalAmount = 1_000m,
            IsSystemGenerated = true,
            ExcludeFromWageCost = true
        };
        fixture.Db.EmployeeWageEntries.Add(entry);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.AnnualService.UpdateWageEntryAsync(
            new UpdateEmployeeWageEntryRequest(
                entry.Id,
                entry.ConcurrencyStamp,
                entry.StartDate,
                entry.EndDate,
                entry.EntryType,
                entry.WageCategory,
                entry.CalculationMethod,
                entry.Nature,
                null,
                null,
                null,
                2_000m,
                null,
                null,
                null,
                0m,
                "尝试修改",
                null,
                "系统记录不可编辑",
                "tester"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*系统生成*");
    }

    [Fact]
    public async Task ExpenseUpdateTreatsAmountAsFinalAndWritesAudit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var expenseId = await fixture.LedgerService.CreateExpenseAsync(
            new CreateExpenseRequest(
                fixture.Employee.Id,
                fixture.Project.Id,
                null,
                fixture.LegalEntity.Id,
                new DateOnly(2026, 7, 2),
                "历史类别",
                1_000m,
                "原备注",
                -100m,
                "FP-OLD"),
            CancellationToken.None);
        var expense = await fixture.Db.ExpenseRecords.SingleAsync(item => item.Id == expenseId);

        var updated = await fixture.LedgerService.UpdateExpenseAsync(
            new UpdateExpenseRequest(
                expense.Id,
                expense.ConcurrencyStamp,
                new DateOnly(2026, 7, 3),
                850m,
                fixture.Project.Id,
                "FP-NEW",
                null,
                "最终报销金额",
                "管理员调整报销金额",
                "tester"),
            CancellationToken.None);

        updated.Amount.Should().Be(850m);
        expense.OriginalAmount.Should().Be(850m);
        expense.AdjustmentAmount.Should().Be(0m);
        (await fixture.Db.AuditLogs.SingleAsync(item => item.EntityId == expense.Id.ToString())).Reason.Should().Be("管理员调整报销金额");
    }

    [Fact]
    public async Task ExpenseUpdateRejectsStaleConcurrencyStamp()
    {
        await using var fixture = await Fixture.CreateAsync();
        var expenseId = await fixture.LedgerService.CreateExpenseAsync(
            new CreateExpenseRequest(
                fixture.Employee.Id,
                null,
                null,
                fixture.LegalEntity.Id,
                new DateOnly(2026, 7, 2),
                "报销",
                500m,
                null),
            CancellationToken.None);

        Func<Task> action = async () => await fixture.LedgerService.UpdateExpenseAsync(
            new UpdateExpenseRequest(
                expenseId,
                Guid.NewGuid(),
                new DateOnly(2026, 7, 2),
                600m,
                null,
                null,
                null,
                null,
                "并发测试",
                "tester"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*已被其他用户修改*");
    }

    [Fact]
    public async Task OtherPayableRowsPreserveDividendInterestAndOtherTypes()
    {
        await using var fixture = await Fixture.CreateAsync();
        foreach (var type in new[] { EmployeeLedgerEntryType.Dividend, EmployeeLedgerEntryType.Interest, EmployeeLedgerEntryType.Other })
        {
            await fixture.LedgerService.CreateOtherPayableAsync(
                new CreateEmployeeOtherPayableRequest(
                    fixture.Employee.Id,
                    null,
                    fixture.LegalEntity.Id,
                    new DateOnly(2026, 7, 5),
                    100m,
                    type,
                    type.ToString()),
                CancellationToken.None);
        }

        var rows = await fixture.LedgerService.GetOtherPayablesAsync(fixture.Employee.Id, CancellationToken.None);

        rows.Select(row => row.EntryType).Should().BeEquivalentTo(new[]
        {
            EmployeeLedgerEntryType.Dividend,
            EmployeeLedgerEntryType.Interest,
            EmployeeLedgerEntryType.Other
        });
    }

    [Fact]
    public async Task OtherPayableCanBeUpdatedWithFinalAmountAndConcurrencyStamp()
    {
        await using var fixture = await Fixture.CreateAsync();
        var id = await fixture.LedgerService.CreateOtherPayableAsync(
            new CreateEmployeeOtherPayableRequest(
                fixture.Employee.Id,
                null,
                fixture.LegalEntity.Id,
                new DateOnly(2026, 7, 5),
                100m,
                EmployeeLedgerEntryType.Dividend,
                "阶段分红"),
            CancellationToken.None);
        var entry = await fixture.Db.EmployeeOtherPayments.SingleAsync(item => item.Id == id);

        var updated = await fixture.LedgerService.UpdateOtherPayableAsync(
            new UpdateEmployeeOtherPayableRequest(
                id,
                entry.ConcurrencyStamp,
                entry.EntryDate,
                250m,
                EmployeeLedgerEntryType.Interest,
                entry.LegalEntityId,
                entry.ProjectId,
                "利息调整",
                "管理员调整利息",
                "tester"),
            CancellationToken.None);

        updated.Amount.Should().Be(250m);
        updated.EntryType.Should().Be(EmployeeLedgerEntryType.Interest);
        (await fixture.Db.AuditLogs.CountAsync(item => item.EntityId == id.ToString())).Should().Be(1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(SqliteConnection connection, ApplicationDbContext db)
        {
            this.connection = connection;
            Db = db;
            AnnualService = new EmployeeAnnualLedgerService(db, new FixedTimeProvider());
            LedgerService = new EmployeeLedgerService(db);
        }

        public ApplicationDbContext Db { get; }
        public EmployeeAnnualLedgerService AnnualService { get; }
        public EmployeeLedgerService LedgerService { get; }
        public Employee Employee { get; private set; } = null!;
        public BusinessYear BusinessYear { get; private set; } = null!;
        public LegalEntity LegalEntity { get; private set; } = null!;
        public Project Project { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, db)
            {
                Employee = new Employee { EmployeeNumber = "PAYABLE-1", Name = "应付测试员工", EmployeeType = EmployeeType.Formal },
                BusinessYear = new BusinessYear { Name = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31) },
                LegalEntity = new LegalEntity { Code = "PAY-LE", Name = "应付测试公司", ShortName = "应付公司" },
                Project = new Project { ProjectNumber = "PAY-P", Name = "应付测试项目", Stage = ProjectStage.UnderConstruction }
            };
            fixture.Project.LegalEntities.Add(new ProjectLegalEntity { Project = fixture.Project, LegalEntity = fixture.LegalEntity, IsPrimary = true });
            db.AddRange(fixture.Employee, fixture.BusinessYear, fixture.LegalEntity, fixture.Project);
            await db.SaveChangesAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.FromHours(8)).ToUniversalTime();
    }
}
