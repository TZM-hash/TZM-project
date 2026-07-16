using EngineeringManager.Application.Dashboard;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Infrastructure.Dashboard;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task EmptyDatabaseReturnsZeroSummary()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.GetAsync(new DashboardActor("admin", true, true, true), default);

        result.ActiveProjectCount.Should().Be(0);
        result.CurrentProjectAmount.Should().Be(0m);
        result.MoneyComparisons.Should().HaveCount(3).And.OnlyContain(item => item.TotalAmount == 0m && item.CompletedAmount == 0m);
        result.UnpaidPayrollAmount.Should().Be(0m);
        result.OpenReminderCount.Should().Be(0);
    }

    [Fact]
    public async Task DashboardAggregatesProjectFinancePayrollAndReminderData()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedBusinessDataAsync();

        var result = await fixture.Service.GetAsync(new DashboardActor("admin", true, true, true), default);

        result.ActiveProjectCount.Should().Be(1);
        result.CurrentProjectAmount.Should().Be(1000m);
        result.StageDistribution.Single().Count.Should().Be(1);
        result.MoneyComparisons.Single(item => item.Key == "receivable").Should().Match<DashboardMoneyComparisonDto>(item => item.TotalAmount == 1000m && item.CompletedAmount == 500m && item.RemainingAmount == 500m);
        result.MoneyComparisons.Single(item => item.Key == "payable").Should().Match<DashboardMoneyComparisonDto>(item => item.TotalAmount == 400m && item.CompletedAmount == 200m && item.RemainingAmount == 180m);
        result.MoneyComparisons.Single(item => item.Key == "invoice").Should().Match<DashboardMoneyComparisonDto>(item => item.TotalAmount == 1000m && item.CompletedAmount == 300m && item.RemainingAmount == 700m);
        result.UnpaidPayrollAmount.Should().Be(150m);
        result.OpenReminderCount.Should().Be(1);
        result.Risks.Should().ContainSingle(item => item.Title == "经营风险");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public DashboardService Service { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext db)
        {
            this.connection = connection;
            Db = db;
            Service = new DashboardService(db);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async Task SeedBusinessDataAsync()
        {
            var legalEntity = new LegalEntity { Code = "LE-01", Name = "自有公司", ShortName = "公司" };
            var partner = new BusinessPartner { PartnerNumber = "BP-DASH", Name = "经营合作单位", ShortName = "合作单位" };
            var project = new Project { ProjectNumber = "P-DASH", Name = "驾驶舱项目", Stage = ProjectStage.UnderConstruction };
            var contract = new Contract { Project = project, ContractNumber = "C-DASH", Name = "主合同", TotalAmount = 1000m };
            contract.LineItems.Add(new ContractLineItem { Contract = contract, Code = "001", Name = "工程量", Unit = "m", EstimatedQuantity = 100m, EstimatedUnitPrice = 10m });
            project.Contracts.Add(contract);
            var account = new FinancialAccount { LegalEntity = legalEntity, AccountName = "基本户", AccountType = FinancialAccountType.Bank };
            var receivable = new ReceivableEntry { Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, SourceType = ReceivableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 1), Amount = 1000m };
            var collection = new CollectionEntry { Receivable = receivable, Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, Account = account, CollectionDate = new DateOnly(2026, 7, 2), Amount = 600m };
            var payable = new PayableEntry { Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, SourceType = PayableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 1), Amount = 400m };
            var payment = new PaymentEntry { Payable = payable, Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, Account = account, PaymentDate = new DateOnly(2026, 7, 3), Amount = 250m };
            Db.AddRange(
                project,
                legalEntity,
                partner,
                account,
                receivable,
                collection,
                new RefundOrReversalEntry { Collection = collection, Receivable = receivable, Account = account, EntryDate = new DateOnly(2026, 7, 4), Amount = 100m, AdjustmentType = FinancialAdjustmentType.Refund, Reason = "退款" },
                payable,
                payment,
                new PaymentReversalEntry { Payment = payment, Account = account, EntryDate = new DateOnly(2026, 7, 4), Amount = 50m, AdjustmentType = FinancialAdjustmentType.Reversal, Reason = "冲销" },
                new DeductionEntry { Payable = payable, Project = project, LegalEntity = legalEntity, BusinessPartner = partner, EntryDate = new DateOnly(2026, 7, 5), Amount = 20m, Reason = "扣款" },
                new InvoiceEntry { Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, Direction = InvoiceDirection.Output, InvoiceNumber = "INV-DASH", InvoiceDate = new DateOnly(2026, 7, 5), GrossAmount = 300m, Status = InvoiceStatus.IssuedOrReceived },
                new ReminderItem { DeduplicationKey = "dashboard-risk", Type = ReminderType.UncollectedReceivable, Severity = ReminderSeverity.Warning, Title = "经营风险", Message = "存在未收款" });

            var employee = new Employee { EmployeeNumber = "E-DASH", Name = "驾驶舱员工", EmployeeType = EmployeeType.Formal };
            var batch = new PayrollBatch { BatchNumber = "PB-DASH", Name = "工资批次", BatchType = PayrollBatchType.Monthly, StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 31), Status = PayrollBatchStatus.Confirmed };
            batch.Items.Add(new PayrollItem { Batch = batch, Employee = employee, ItemType = PayrollItemType.FixedSalary, Nature = PayrollItemNature.Earning, Amount = 300m });
            batch.Items.Add(new PayrollItem { Batch = batch, Employee = employee, ItemType = PayrollItemType.Penalty, Nature = PayrollItemNature.Deduction, Amount = 50m });
            batch.Payments.Add(new PayrollPayment { Batch = batch, Employee = employee, Account = account, PaymentDate = new DateOnly(2026, 7, 10), Amount = 100m, PayeeType = PayrollPayeeType.Employee, PayeeName = employee.Name });
            Db.AddRange(employee, batch);
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
