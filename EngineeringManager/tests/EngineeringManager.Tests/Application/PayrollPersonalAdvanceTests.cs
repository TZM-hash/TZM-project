using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Payroll;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class PayrollPersonalAdvanceTests
{
    [Fact]
    public async Task PersonallyFundedBatchCreatesOneReadOnlyOwnerPayableExcludedFromWageCost()
    {
        await using var fixture = await Fixture.CreateAsync();

        var batch = await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.Request(
                "ADVANCE-001",
                1_000m,
                fixture.PersonalAccount.Id,
                [Fixture.WageLine(fixture.Recipient.Id, 1_000m)]) with
            {
                FundingSource = PayrollFundingSource.PersonalAdvance
            },
            CancellationToken.None);

        var payable = await fixture.Db.EmployeeWageEntries.SingleAsync(item => item.SourcePersonalAdvanceBatchId == batch.Batch.Id);
        payable.EmployeeId.Should().Be(fixture.Owner.Id);
        payable.EntryType.Should().Be(EmployeeWageEntryType.Other);
        payable.FinalAmount.Should().Be(1_000m);
        payable.IsSystemGenerated.Should().BeTrue();
        payable.ExcludeFromWageCost.Should().BeTrue();
        payable.Notes.Should().Contain("个人垫付待归还");
        (await fixture.Db.AccountTransactions.CountAsync(item => item.SourceType == AccountTransactionSourceType.PayrollPayment)).Should().Be(1);

        await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.Request(
                "ADVANCE-001",
                1_000m,
                fixture.PersonalAccount.Id,
                batch.Lines.Select(Fixture.CopyLine).ToArray()) with
            {
                Id = batch.Batch.Id,
                ConcurrencyStamp = batch.Batch.ConcurrencyStamp,
                FundingSource = PayrollFundingSource.PersonalAdvance,
                Status = PayrollBatchStatus.Voided,
                Reason = "撤销个人垫付"
            },
            CancellationToken.None);

        (await fixture.Db.EmployeeWageEntries.SingleAsync(item => item.Id == payable.Id)).FinalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task MigrantWageRequiresLaborCompanyAndAllowsNullProject()
    {
        await using var fixture = await Fixture.CreateAsync();
        var missingLabor = Fixture.WageLine(fixture.Recipient.Id, 800m) with
        {
            WageCategory = EmployeeWageCategory.MigrantWorkerWage
        };

        Func<Task> action = async () => await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.Request("MIGRANT-FAIL", 800m, fixture.CompanyAccount.Id, [missingLabor]),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*劳务公司*");

        var saved = await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.Request(
                "MIGRANT-OK",
                800m,
                fixture.CompanyAccount.Id,
                [missingLabor with { LaborBusinessPartnerId = fixture.LaborCompany.Id }]),
            CancellationToken.None);

        saved.Batch.ProjectId.Should().BeNull();
        saved.Lines.Should().ContainSingle().Which.LaborBusinessPartnerId.Should().Be(fixture.LaborCompany.Id);
    }

    [Fact]
    public async Task CompanyRepaymentCreatesPersonalAccountInflowWithoutDuplicatePaymentAllocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.Request(
                "ADVANCE-SOURCE",
                1_000m,
                fixture.PersonalAccount.Id,
                [Fixture.WageLine(fixture.Recipient.Id, 1_000m)]) with
            {
                FundingSource = PayrollFundingSource.PersonalAdvance
            },
            CancellationToken.None);

        var repayment = await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.Request(
                "ADVANCE-REPAY",
                400m,
                fixture.CompanyAccount.Id,
                [Fixture.WageLine(fixture.Owner.Id, 400m) with
                {
                    PaymentCategory = PayrollPaymentCategory.Other,
                    WageCategory = null
                }]) with
            {
                DisbursementType = PayrollDisbursementType.Other,
                RepaysPersonalAdvanceAccountId = fixture.PersonalAccount.Id
            },
            CancellationToken.None);

        repayment.Lines.Should().ContainSingle();
        (await fixture.Db.PayrollPayments.CountAsync(item => item.PayrollBatchId == repayment.Batch.Id)).Should().Be(1);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.SourceId == repayment.Batch.Id)).Should().Be(2);
        (await fixture.Db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.PersonalAdvanceRepayment)).Should().Match<AccountTransaction>(item =>
            item.AccountId == fixture.PersonalAccount.Id &&
            item.Direction == AccountTransactionDirection.Inflow &&
            item.Amount == 400m);

        var personalTransactions = fixture.Db.AccountTransactions.Where(item => item.AccountId == fixture.PersonalAccount.Id);
        var advanced = await personalTransactions.Where(item => item.Direction == AccountTransactionDirection.Outflow).SumAsync(item => item.Amount);
        var repaid = await personalTransactions.Where(item => item.Direction == AccountTransactionDirection.Inflow).SumAsync(item => item.Amount);
        (advanced - repaid).Should().Be(600m);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(SqliteConnection connection, ApplicationDbContext db)
        {
            this.connection = connection;
            Db = db;
            Service = new PayrollService(db);
        }

        public ApplicationDbContext Db { get; }
        public PayrollService Service { get; }
        public LegalEntity Company { get; private set; } = null!;
        public Employee Owner { get; private set; } = null!;
        public Employee Recipient { get; private set; } = null!;
        public FinancialAccount CompanyAccount { get; private set; } = null!;
        public FinancialAccount PersonalAccount { get; private set; } = null!;
        public BusinessPartner LaborCompany { get; private set; } = null!;

        public SavePayrollDisbursementBatchRequest Request(
            string number,
            decimal amount,
            Guid accountId,
            IReadOnlyList<PayrollDisbursementLineRequest> lines) => new(
                null,
                number,
                number,
                new DateOnly(2026, 7, 20),
                null,
                Company.Id,
                accountId,
                amount,
                PaymentMethod.BankTransfer,
                null,
                PayrollBatchStatus.Confirmed,
                null,
                null,
                "测试工资台账联动",
                lines,
                []);

        public static PayrollDisbursementLineRequest WageLine(Guid employeeId, decimal amount) => new(
            null,
            PayrollRecipientType.Employee,
            employeeId,
            null,
            null,
            amount,
            null,
            PayrollPaymentCategory.Wage,
            EmployeeWageCategory.SocialSecurityWage,
            null,
            null);

        public static PayrollDisbursementLineRequest CopyLine(PayrollDisbursementLineDto line) => new(
            line.Id,
            line.RecipientType,
            line.EmployeeId,
            line.ConstructionWorkerId,
            line.CrewBusinessPartnerId,
            line.Amount,
            line.Notes,
            line.PaymentCategory,
            line.WageCategory,
            line.LaborBusinessPartnerId,
            line.ProjectId);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, db)
            {
                Company = new LegalEntity { Code = "ADV-LE", Name = "垫付测试公司", ShortName = "垫付公司" },
                Owner = new Employee { EmployeeNumber = "ADV-OWNER", Name = "张三", EmployeeType = EmployeeType.Formal },
                Recipient = new Employee { EmployeeNumber = "ADV-RECIPIENT", Name = "李四", EmployeeType = EmployeeType.Formal },
                LaborCompany = new BusinessPartner { PartnerNumber = "ADV-LABOR", Name = "测试劳务公司", ShortName = "劳务公司" }
            };
            fixture.LaborCompany.Roles.Add(new BusinessPartnerRole { Partner = fixture.LaborCompany, RoleType = BusinessPartnerRoleType.ConstructionCrew });
            fixture.CompanyAccount = new FinancialAccount
            {
                LegalEntity = fixture.Company,
                AccountName = "公司账户",
                AccountType = FinancialAccountType.Bank
            };
            fixture.PersonalAccount = new FinancialAccount
            {
                LegalEntity = fixture.Company,
                AccountName = "张三个人垫付账户",
                AccountType = FinancialAccountType.PersonalAdvance,
                OwnerEmployee = fixture.Owner,
                OwnerName = fixture.Owner.Name
            };
            var year = new BusinessYear { Name = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31) };
            db.AddRange(fixture.Company, fixture.Owner, fixture.Recipient, fixture.LaborCompany, fixture.CompanyAccount, fixture.PersonalAccount, year);
            await db.SaveChangesAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
