using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.EmployeeAnnualLedger;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class EmployeeAnnualLedgerAggregationTests
{
    [Fact]
    public async Task AggregatesLegacyAndNewRecordsWithoutCountingLinkedPayrollItemTwice()
    {
        await using var fixture = await AggregationFixture.CreateAsync();
        var batch = new PayrollBatch
        {
            BatchNumber = "LEGACY-PAY",
            Name = "旧工资批次",
            BatchType = PayrollBatchType.Monthly,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 31),
            LegalEntity = fixture.LegalEntity
        };
        var linkedLegacyItem = new PayrollItem
        {
            Batch = batch,
            Employee = fixture.Employee,
            ItemType = PayrollItemType.FixedSalary,
            Nature = PayrollItemNature.Earning,
            Amount = 1_000m
        };
        batch.Items.Add(linkedLegacyItem);
        batch.Items.Add(new PayrollItem
        {
            Batch = batch,
            Employee = fixture.Employee,
            ItemType = PayrollItemType.LeaveDeduction,
            Nature = PayrollItemNature.Deduction,
            Amount = 100m
        });
        var linkedNewEntry = new EmployeeWageEntry
        {
            Employee = fixture.Employee,
            BusinessYear = fixture.BusinessYear,
            StartDate = batch.StartDate,
            EndDate = batch.EndDate,
            WageCategory = EmployeeWageCategory.SocialSecurityWage,
            CalculationMethod = EmployeeWageCalculationMethod.FixedAmount,
            Nature = PayrollItemNature.Earning,
            AutomaticAmount = 1_000m,
            FinalAmount = 1_000m,
            SourcePayrollItem = linkedLegacyItem
        };
        var directNewEntry = new EmployeeWageEntry
        {
            Employee = fixture.Employee,
            BusinessYear = fixture.BusinessYear,
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2026, 8, 31),
            WageCategory = EmployeeWageCategory.MigrantWorkerWage,
            CalculationMethod = EmployeeWageCalculationMethod.FixedAmount,
            Nature = PayrollItemNature.Earning,
            AutomaticAmount = 2_000m,
            FinalAmount = 2_000m
        };
        var payrollPayment = new PayrollPayment
        {
            Batch = batch,
            Employee = fixture.Employee,
            Account = fixture.Account,
            PaymentDate = new DateOnly(2026, 8, 5),
            Amount = 500m,
            PayeeType = PayrollPayeeType.Employee,
            PayeeName = fixture.Employee.Name
        };
        var expense = new ExpenseRecord
        {
            Employee = fixture.Employee,
            LegalEntity = fixture.LegalEntity,
            ExpenseDate = new DateOnly(2026, 7, 10),
            Category = "交通费",
            Amount = 400m
        };
        expense.Payments.Add(new ExpensePayment
        {
            Expense = expense,
            Account = fixture.Account,
            PaymentDate = new DateOnly(2026, 7, 15),
            Amount = 100m,
            PaymentMethod = PaymentMethod.BankTransfer,
            RecordKind = EmployeeLedgerRecordKind.Payment
        });
        var otherPayable = new EmployeeOtherPayment
        {
            Employee = fixture.Employee,
            LegalEntity = fixture.LegalEntity,
            EntryType = EmployeeLedgerEntryType.Dividend,
            RecordKind = EmployeeLedgerRecordKind.Payable,
            EntryDate = new DateOnly(2026, 9, 1),
            Amount = 200m
        };
        var otherPayment = new EmployeeOtherPayment
        {
            Employee = fixture.Employee,
            LegalEntity = fixture.LegalEntity,
            EntryType = EmployeeLedgerEntryType.Dividend,
            RecordKind = EmployeeLedgerRecordKind.Payment,
            RelatedPayable = otherPayable,
            Account = fixture.Account,
            EntryDate = new DateOnly(2026, 9, 5),
            Amount = 50m,
            PaymentMethod = PaymentMethod.BankTransfer
        };
        var adjustment = new EmployeeFinancialAdjustment
        {
            Employee = fixture.Employee,
            BusinessYear = fixture.BusinessYear,
            AdjustmentDate = new DateOnly(2026, 10, 1),
            Amount = 25m,
            AdjustmentType = EmployeeFinancialAdjustmentType.AdministratorAdjustment,
            Notes = "补差"
        };
        var receipt = new EmployeeReceipt
        {
            Employee = fixture.Employee,
            BusinessYear = fixture.BusinessYear,
            ReceiptDate = new DateOnly(2026, 10, 2),
            ReceiptType = EmployeeReceiptType.General,
            Amount = 300m,
            PaymentLegalEntity = fixture.LegalEntity,
            Account = fixture.Account,
            PaymentMethod = PaymentMethod.BankTransfer,
            ActualRecipientName = fixture.Employee.Name
        };
        fixture.Db.AddRange(batch, linkedNewEntry, directNewEntry, payrollPayment, expense, otherPayable, otherPayment, adjustment, receipt);
        await fixture.Db.SaveChangesAsync();

        var ledger = await fixture.Service.GetAnnualLedgerAsync(fixture.Employee.Id, fixture.BusinessYear.Id, CancellationToken.None);

        ledger.Summary.CurrentYearWagePayable.Should().Be(2_900m);
        ledger.Summary.ExpensePayable.Should().Be(400m);
        ledger.Summary.OtherPayable.Should().Be(200m);
        ledger.Summary.AdjustmentAmount.Should().Be(25m);
        ledger.Summary.ReceivedAmount.Should().Be(950m);
        ledger.Summary.CurrentBalance.Should().Be(2_575m);
        ledger.PayableLines.Where(item => item.Category == AnnualLedgerEntryCategory.Wage).Should().HaveCount(3);
        ledger.PayableLines.Should().Contain(item => item.Category == AnnualLedgerEntryCategory.Expense && item.Amount == 400m);
        ledger.PayableLines.Should().Contain(item => item.Category == AnnualLedgerEntryCategory.OtherPayable && item.Amount == 200m);
        ledger.PayableLines.Should().Contain(item => item.Amount == 2_000m && item.IsUnassigned);
        ledger.ReceiptLines.Should().HaveCount(4);
    }

    [Fact]
    public async Task ReceiptCanBeAddedToHistoricalYearAndCreatesOneCashOutflow()
    {
        await using var fixture = await AggregationFixture.CreateAsync();

        var receipt = await fixture.Service.RecordReceiptAsync(
            new RecordEmployeeReceiptRequest(
                fixture.Employee.Id,
                fixture.BusinessYear.Id,
                new DateOnly(2026, 7, 18),
                EmployeeReceiptType.Wage,
                800m,
                fixture.LegalEntity.Id,
                fixture.Account.Id,
                PaymentMethod.BankTransfer,
                "代收人",
                null,
                null,
                "补发旧工资"),
            CancellationToken.None);

        receipt.Amount.Should().Be(800m);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.SourceType == AccountTransactionSourceType.EmployeeReceipt)).Should().Be(1);
    }

    [Fact]
    public async Task UnifiedPayrollEmployeeLineEntersReceiptsOnceAndKeepsSourceLink()
    {
        await using var fixture = await AggregationFixture.CreateAsync();
        var batch = new PayrollBatch
        {
            BatchNumber = "UNIFIED-RECEIPT",
            Name = "统一工资发放",
            BatchType = PayrollBatchType.Temporary,
            StartDate = new DateOnly(2026, 7, 18),
            EndDate = new DateOnly(2026, 7, 18),
            PaymentDate = new DateOnly(2026, 7, 18),
            LegalEntity = fixture.LegalEntity,
            Account = fixture.Account,
            ActualAmount = 800m,
            IsUnifiedDisbursement = true,
            Status = PayrollBatchStatus.Confirmed
        };
        var line = new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.Employee,
            RecipientKey = $"employee:{fixture.Employee.Id:N}",
            Employee = fixture.Employee,
            Amount = 800m,
            PayeeName = fixture.Employee.Name,
            RecipientNameSnapshot = fixture.Employee.Name
        };
        batch.Payments.Add(line);
        fixture.Db.PayrollBatches.Add(batch);
        await fixture.Db.SaveChangesAsync();

        var ledger = await fixture.Service.GetAnnualLedgerAsync(fixture.Employee.Id, fixture.BusinessYear.Id, CancellationToken.None);

        ledger.Summary.ReceivedAmount.Should().Be(800m);
        ledger.ReceiptLines.Should().ContainSingle().Which.Should().Match<EmployeeAnnualLedgerReceiptLineDto>(item =>
            item.SourceType == "PayrollDisbursement" && item.PayrollBatchId == batch.Id && item.PayrollPaymentId == line.Id);
        (await fixture.Db.EmployeeReceipts.CountAsync()).Should().Be(0);
    }

    private sealed class AggregationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private AggregationFixture(
            SqliteConnection connection,
            ApplicationDbContext db,
            EmployeeAnnualLedgerService service,
            Employee employee,
            BusinessYear businessYear,
            LegalEntity legalEntity,
            FinancialAccount account)
        {
            this.connection = connection;
            Db = db;
            Service = service;
            Employee = employee;
            BusinessYear = businessYear;
            LegalEntity = legalEntity;
            Account = account;
        }

        public ApplicationDbContext Db { get; }
        public EmployeeAnnualLedgerService Service { get; }
        public Employee Employee { get; }
        public BusinessYear BusinessYear { get; }
        public LegalEntity LegalEntity { get; }
        public FinancialAccount Account { get; }

        public static async Task<AggregationFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var employee = new Employee { EmployeeNumber = "AGG-E", Name = "聚合员工", EmployeeType = EmployeeType.Formal };
            var year = new BusinessYear { Name = "聚合年度", StartDate = new DateOnly(2026, 3, 1), EndDate = new DateOnly(2027, 2, 28) };
            var legalEntity = new LegalEntity { Code = "AGG-LE", Name = "聚合公司", ShortName = "聚合公司" };
            var account = new FinancialAccount { LegalEntity = legalEntity, AccountName = "聚合账户", AccountType = FinancialAccountType.Bank };
            db.AddRange(employee, year, legalEntity, account);
            await db.SaveChangesAsync();
            var timeProvider = new FixedTimeProvider(new DateTimeOffset(2028, 7, 18, 0, 0, 0, TimeSpan.FromHours(8)));
            return new AggregationFixture(connection, db, new EmployeeAnnualLedgerService(db, timeProvider), employee, year, legalEntity, account);
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
