using EngineeringManager.Application.EmployeeLedger;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.EmployeeLedger;
using EngineeringManager.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class EmployeeLedgerServiceTests
{
    [Fact]
    public async Task ExpensesAdvancesDividendsAndInterestUpdateBalancesAndCashFlow()
    {
        await using var fixture = await EmployeeLedgerFixture.CreateAsync();
        var expenseId = await fixture.Service.CreateExpenseAsync(
            new CreateExpenseRequest(fixture.Employee.Id, fixture.Project.Id, fixture.Department.Id, fixture.LegalEntity.Id, new DateOnly(2026, 7, 10), "交通费", 1000m, "项目出差"),
            CancellationToken.None);
        await fixture.Service.RecordExpensePaymentAsync(
            new RecordExpensePaymentRequest(expenseId, fixture.Account.Id, new DateOnly(2026, 7, 15), 800m, PaymentMethod.BankTransfer, EmployeeLedgerRecordKind.Payment, "报销支付"),
            CancellationToken.None);
        await fixture.Service.RecordExpensePaymentAsync(
            new RecordExpensePaymentRequest(expenseId, fixture.Account.Id, new DateOnly(2026, 7, 16), 100m, PaymentMethod.BankTransfer, EmployeeLedgerRecordKind.RefundOrReversal, "退回"),
            CancellationToken.None);
        await fixture.Service.RecordAdvanceAsync(new RecordEmployeeAdvanceRequest(fixture.Employee.Id, fixture.Project.Id, fixture.LegalEntity.Id, fixture.Account.Id, new DateOnly(2026, 7, 1), 2000m, EmployeeAdvanceAction.Disbursement, "备用金"), CancellationToken.None);
        await fixture.Service.RecordAdvanceAsync(new RecordEmployeeAdvanceRequest(fixture.Employee.Id, fixture.Project.Id, fixture.LegalEntity.Id, fixture.Account.Id, new DateOnly(2026, 7, 20), 500m, EmployeeAdvanceAction.Repayment, "归还"), CancellationToken.None);
        await fixture.Service.RecordAdvanceAsync(new RecordEmployeeAdvanceRequest(fixture.Employee.Id, fixture.Project.Id, fixture.LegalEntity.Id, null, new DateOnly(2026, 7, 31), 300m, EmployeeAdvanceAction.PayrollDeduction, "工资抵扣"), CancellationToken.None);
        var dividendId = await fixture.Service.CreateOtherPayableAsync(new CreateEmployeeOtherPayableRequest(fixture.Employee.Id, fixture.Project.Id, fixture.LegalEntity.Id, new DateOnly(2026, 7, 31), 1000m, EmployeeLedgerEntryType.Dividend, "阶段分红"), CancellationToken.None);
        await fixture.Service.RecordOtherPaymentAsync(new RecordEmployeeOtherPaymentRequest(dividendId, fixture.Account.Id, new DateOnly(2026, 8, 1), 1200m, PaymentMethod.BankTransfer, EmployeeLedgerRecordKind.Payment, "分红支付"), CancellationToken.None);

        var summary = await fixture.Service.GetEmployeeSummaryAsync(fixture.Employee.Id, CancellationToken.None);
        var overview = await fixture.Service.GetOverviewAsync(CancellationToken.None);

        summary.ExpensePaidAmount.Should().Be(700m);
        summary.ExpenseUnpaidAmount.Should().Be(300m);
        summary.AdvanceOutstandingAmount.Should().Be(1200m);
        summary.OtherUnpaidAmount.Should().Be(-200m);
        summary.HasOtherOverpaymentRisk.Should().BeTrue();
        overview.ExpenseUnpaidAmount.Should().Be(300m);
        overview.AdvanceOutstandingAmount.Should().Be(1200m);
        overview.EmployeeSummaries.Should().ContainSingle(item => item.EmployeeId == fixture.Employee.Id);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.Direction == AccountTransactionDirection.Outflow)).Should().Be(3);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.Direction == AccountTransactionDirection.Inflow)).Should().Be(2);
    }

    [Fact]
    public async Task PayrollDeductionDoesNotCreateDuplicateCashTransaction()
    {
        await using var fixture = await EmployeeLedgerFixture.CreateAsync();

        await fixture.Service.RecordAdvanceAsync(new RecordEmployeeAdvanceRequest(fixture.Employee.Id, null, fixture.LegalEntity.Id, null, new DateOnly(2026, 7, 31), 300m, EmployeeAdvanceAction.PayrollDeduction, "工资抵扣"), CancellationToken.None);

        (await fixture.Db.EmployeeAdvances.CountAsync()).Should().Be(1);
        (await fixture.Db.AccountTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExpenseStoresOriginalAdjustmentFinalAmountReceiptNumberAndAttachment()
    {
        await using var fixture = await EmployeeLedgerFixture.CreateAsync(new FakeFileStore());

        var expenseId = await fixture.Service.CreateExpenseAsync(
            new CreateExpenseRequest(
                fixture.Employee.Id,
                fixture.Project.Id,
                fixture.Department.Id,
                fixture.LegalEntity.Id,
                new DateOnly(2026, 7, 10),
                "材料费",
                1_000m,
                "采购报销",
                -100m,
                "FP-2026-001",
                new ExpenseAttachmentUpload("invoice.pdf", "application/pdf", [1, 2, 3])),
            CancellationToken.None);

        var expense = await fixture.Db.ExpenseRecords.Include(item => item.Attachment).SingleAsync(item => item.Id == expenseId);
        expense.OriginalAmount.Should().Be(1_000m);
        expense.AdjustmentAmount.Should().Be(-100m);
        expense.Amount.Should().Be(900m);
        expense.ReceiptNumber.Should().Be("FP-2026-001");
        expense.Attachment.Should().NotBeNull();
        expense.Attachment!.OriginalFileName.Should().Be("invoice.pdf");
    }

    private sealed class EmployeeLedgerFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private EmployeeLedgerFixture(SqliteConnection connection, ApplicationDbContext db, IEmployeeLedgerService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IEmployeeLedgerService Service { get; }
        public Employee Employee { get; private set; } = null!;
        public Project Project { get; private set; } = null!;
        public OrganizationUnit Department { get; private set; } = null!;
        public LegalEntity LegalEntity { get; private set; } = null!;
        public FinancialAccount Account { get; private set; } = null!;

        public static async Task<EmployeeLedgerFixture> CreateAsync(IFileStore? fileStore = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new EmployeeLedgerFixture(connection, db, new EmployeeLedgerService(db, fileStore));
            fixture.Employee = new Employee { EmployeeNumber = "LEDGER-E", Name = "往来测试员工", EmployeeType = EmployeeType.Formal };
            fixture.Project = new Project { ProjectNumber = "LEDGER-P", Name = "往来测试项目", Stage = ProjectStage.UnderConstruction };
            fixture.Department = new OrganizationUnit { Code = "LEDGER-D", Name = "往来测试部门", UnitType = OrganizationUnitType.Department };
            fixture.LegalEntity = new LegalEntity { Code = "LEDGER-LE", Name = "往来测试公司", ShortName = "往来公司" };
            fixture.Project.LegalEntities.Add(new ProjectLegalEntity { Project = fixture.Project, LegalEntity = fixture.LegalEntity, IsPrimary = true });
            fixture.Account = new FinancialAccount { LegalEntity = fixture.LegalEntity, AccountName = "员工往来账户", AccountType = FinancialAccountType.Bank, OpeningBalance = 10000m };
            db.AddRange(fixture.Employee, fixture.Project, fixture.Department, fixture.LegalEntity, fixture.Account);
            await db.SaveChangesAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }


    private sealed class FakeFileStore : IFileStore
    {
        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken) => Task.FromResult("stored-invoice.pdf");
        public Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(string storedName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
