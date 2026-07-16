using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Payroll;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class PayrollPaymentTests
{
    [Fact]
    public async Task MultiplePayrollPaymentsCreateAccountOutflowsAndUpdateSummary()
    {
        await using var fixture = await PayrollPaymentFixture.CreateAsync();

        var firstPaymentId = await fixture.Service.RecordPaymentAsync(
            new RecordPayrollPaymentRequest(fixture.Batch.Id, fixture.Employee.Id, fixture.Account.Id, new DateOnly(2026, 8, 1), 3000m, PaymentMethod.BankTransfer, PayrollPayeeType.Employee, fixture.Employee.Name, null, "第一笔"),
            CancellationToken.None);
        var secondPaymentId = await fixture.Service.RecordPaymentAsync(
            new RecordPayrollPaymentRequest(fixture.Batch.Id, fixture.Employee.Id, fixture.Account.Id, new DateOnly(2026, 8, 5), 1000m, PaymentMethod.WeChat, PayrollPayeeType.CrewLeader, "班组负责人", fixture.Crew.Id, "代收"),
            CancellationToken.None);

        var summary = await fixture.Service.GetBatchSummaryAsync(fixture.Batch.Id, CancellationToken.None);
        summary.PaidAmount.Should().Be(4000m);
        summary.UnpaidAmount.Should().Be(1000m);
        summary.HasOverpaymentRisk.Should().BeFalse();
        var transactions = await fixture.Db.AccountTransactions.Where(item => item.SourceType == AccountTransactionSourceType.PayrollPayment).ToListAsync();
        transactions.Should().HaveCount(2);
        transactions.Should().ContainSingle(item => item.SourceId == firstPaymentId && item.Amount == 3000m && item.Direction == AccountTransactionDirection.Outflow);
        transactions.Should().ContainSingle(item => item.SourceId == secondPaymentId && item.Amount == 1000m && item.Direction == AccountTransactionDirection.Outflow);
    }

    [Fact]
    public async Task PayrollOverpaymentIsSavedAndFlagged()
    {
        await using var fixture = await PayrollPaymentFixture.CreateAsync();

        await fixture.Service.RecordPaymentAsync(
            new RecordPayrollPaymentRequest(fixture.Batch.Id, fixture.Employee.Id, fixture.Account.Id, new DateOnly(2026, 8, 1), 5200m, PaymentMethod.BankTransfer, PayrollPayeeType.EntrustedRecipient, "受托收款人", null, null),
            CancellationToken.None);

        var summary = await fixture.Service.GetBatchSummaryAsync(fixture.Batch.Id, CancellationToken.None);
        summary.UnpaidAmount.Should().Be(-200m);
        summary.HasOverpaymentRisk.Should().BeTrue();
    }

    private sealed class PayrollPaymentFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private PayrollPaymentFixture(SqliteConnection connection, ApplicationDbContext db, IPayrollService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IPayrollService Service { get; }
        public LegalEntity LegalEntity { get; private set; } = null!;
        public Employee Employee { get; private set; } = null!;
        public PayrollBatchDto Batch { get; private set; } = null!;
        public FinancialAccount Account { get; private set; } = null!;
        public BusinessPartner Crew { get; private set; } = null!;

        public static async Task<PayrollPaymentFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new PayrollPaymentFixture(connection, db, new PayrollService(db));
            fixture.LegalEntity = new LegalEntity { Code = "PAYMENT-LE", Name = "发薪测试公司", ShortName = "发薪公司" };
            fixture.Employee = new Employee { EmployeeNumber = "PAYMENT-E", Name = "发薪测试员工", EmployeeType = EmployeeType.Labor };
            fixture.Account = new FinancialAccount { LegalEntity = fixture.LegalEntity, AccountName = "发薪账户", AccountType = FinancialAccountType.Bank, OpeningBalance = 10000m };
            fixture.Crew = new BusinessPartner { PartnerNumber = "PAYMENT-CREW", Name = "发薪测试班组", ShortName = "发薪班组" };
            fixture.Crew.Roles.Add(new BusinessPartnerRole { Partner = fixture.Crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
            db.AddRange(fixture.LegalEntity, fixture.Employee, fixture.Account, fixture.Crew);
            await db.SaveChangesAsync();
            fixture.Batch = await fixture.Service.CreateBatchAsync(new CreatePayrollBatchRequest("PAYMENT-B", "发薪批次", PayrollBatchType.Monthly, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), null, fixture.LegalEntity.Id, null), CancellationToken.None);
            await fixture.Service.AddItemAsync(new CreatePayrollItemRequest(fixture.Batch.Id, fixture.Employee.Id, PayrollItemType.FixedSalary, PayrollItemNature.Earning, null, null, 5000m, null, []), CancellationToken.None);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
