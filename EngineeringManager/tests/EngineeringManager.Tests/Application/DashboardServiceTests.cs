using EngineeringManager.Application.Dashboard;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
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
        result.CashWatchlist.Should().ContainSingle().Which.Should().Match<DashboardProjectCashDto>(item =>
            item.ProjectNumber == "P-DASH" &&
            item.CollectedAmount == 500m &&
            item.PaidAmount == 200m &&
            item.UncollectedAmount == 500m &&
            item.UnpaidAmount == 180m &&
            item.CashGap == 300m);
    }

    [Fact]
    public async Task DashboardFinanceUsesOnlyProjectsAuthorizedForActor()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedBusinessDataAsync();
        fixture.Db.Users.AddRange(
            new ApplicationUser { Id = "project-user", UserName = "project-user" },
            new ApplicationUser { Id = "another-user", UserName = "another-user" });
        var authorized = await fixture.Db.Projects.SingleAsync(item => item.ProjectNumber == "P-DASH");
        authorized.ResponsibleUserId = "project-user";
        fixture.Db.Projects.Add(new Project
        {
            ProjectNumber = "P-HIDDEN",
            Name = "无权项目",
            Stage = ProjectStage.UnderConstruction,
            ResponsibleUserId = "another-user"
        });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetAsync(new DashboardActor("project-user", false, true, false), default);

        result.CashWatchlist.Should().ContainSingle(item => item.ProjectId == authorized.Id);
        result.CashWatchlist.Should().NotContain(item => item.ProjectNumber == "P-HIDDEN");
        result.MoneyComparisons.Single(item => item.Key == "receivable").TotalAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task DashboardProvidesTwelveMonthTrendEquipmentAndPayrollSummary()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedBusinessDataAsync();

        var result = await fixture.Service.GetAsync(new DashboardActor("admin", true, true, true), default);

        result.MonthlyTrend.Should().HaveCount(12);
        result.MonthlyTrend.Should().Contain(item => item.Collected > 0m && item.Paid > 0m);
        result.EquipmentSummary.Total.Should().BeGreaterThan(0);
        result.EquipmentSummary.RentedCost.Should().BeGreaterThan(0m);
        result.PayrollSummary.Unpaid.Should().BeGreaterThanOrEqualTo(0m);
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
            var contract = new Contract { Project = project, BusinessPartner = partner, ContractNumber = "C-DASH", Name = "主合同", TotalAmount = 1000m };
            contract.LineItems.Add(new ContractLineItem { Contract = contract, Code = "001", Name = "工程量", Unit = "m", EstimatedQuantity = 100m, EstimatedUnitPrice = 10m });
            project.Contracts.Add(contract);
            project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = legalEntity, IsPrimary = true });
            var account = new FinancialAccount { LegalEntity = legalEntity, AccountName = "基本户", AccountType = FinancialAccountType.Bank };
            var receivable = new FinanceSettlement { Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = LedgerSettlementState.Final, SourceType = LedgerSourceType.CentralLedger, Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 1000m, OriginalInvoiceAmount = 1000m };
            var collection = new FinanceCashEntry { Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, CashType = LedgerCashType.Collection, LegalEntity = legalEntity, BusinessPartner = partner, Account = account, BusinessDate = new DateOnly(2026, 7, 2), Amount = 600m };
            collection.Allocations.Add(new FinanceCashAllocation { CashEntry = collection, Settlement = receivable, Project = project, Contract = contract, Amount = 600m, AllocationOrder = 1 });
            var refund = new FinanceCashEntry { Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, CashType = LedgerCashType.Collection, LegalEntity = legalEntity, BusinessPartner = partner, Account = account, IsReversal = true, ReversesCashEntry = collection, BusinessDate = new DateOnly(2026, 7, 4), Amount = 100m, Notes = "退款" };
            refund.Allocations.Add(new FinanceCashAllocation { CashEntry = refund, Settlement = receivable, Project = project, Contract = contract, Amount = 100m, AllocationOrder = 1 });
            var payable = new FinanceSettlement { Scope = LedgerScope.External, Direction = LedgerDirection.Payable, SettlementState = LedgerSettlementState.Final, SourceType = LedgerSourceType.CentralLedger, Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 400m, OriginalInvoiceAmount = 400m };
            payable.Deductions.Add(new FinanceDeduction { Settlement = payable, BusinessDate = new DateOnly(2026, 7, 5), Amount = 20m, Reason = "扣款" });
            var payment = new FinanceCashEntry { Scope = LedgerScope.External, Direction = LedgerDirection.Payable, CashType = LedgerCashType.Payment, LegalEntity = legalEntity, BusinessPartner = partner, Account = account, BusinessDate = new DateOnly(2026, 7, 3), Amount = 250m };
            payment.Allocations.Add(new FinanceCashAllocation { CashEntry = payment, Settlement = payable, Project = project, Contract = contract, Amount = 250m, AllocationOrder = 1 });
            var paymentReversal = new FinanceCashEntry { Scope = LedgerScope.External, Direction = LedgerDirection.Payable, CashType = LedgerCashType.Payment, LegalEntity = legalEntity, BusinessPartner = partner, Account = account, IsReversal = true, ReversesCashEntry = payment, BusinessDate = new DateOnly(2026, 7, 4), Amount = 50m, Notes = "冲销" };
            paymentReversal.Allocations.Add(new FinanceCashAllocation { CashEntry = paymentReversal, Settlement = payable, Project = project, Contract = contract, Amount = 50m, AllocationOrder = 1 });
            var invoice = new FinanceInvoice { Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, LegalEntity = legalEntity, BusinessPartner = partner, InvoiceNumber = "INV-DASH", InvoiceDate = new DateOnly(2026, 7, 5), Amount = 300m };
            invoice.Allocations.Add(new FinanceInvoiceAllocation { Invoice = invoice, Settlement = receivable, Project = project, Contract = contract, Amount = 300m, AllocationOrder = 1 });
            Db.AddRange(
                project,
                legalEntity,
                partner,
                account,
                receivable,
                collection,
                refund,
                payable,
                payment,
                paymentReversal,
                invoice,
                new ReminderItem { DeduplicationKey = "dashboard-risk", Type = ReminderType.UncollectedReceivable, Severity = ReminderSeverity.Warning, Title = "经营风险", Message = "存在未收款" });

            var equipment = new Equipment { EquipmentNumber = "EQ-DASH", Name = "租赁吊车", OwnershipType = EquipmentOwnershipType.Rented, Status = EquipmentStatus.InUse };
            var usage = new EquipmentProjectUsage { Equipment = equipment, Project = project, LegalEntity = legalEntity, EntryDate = new DateOnly(2026, 7, 1), UnitRate = 100m, RentMode = RentMode.Daily };
            usage.Settlement = new EquipmentSettlement { Usage = usage, SettlementDate = new DateOnly(2026, 7, 31), BaseAmount = 1000m, TotalAmount = 1200m };
            equipment.ProjectUsages.Add(usage);
            Db.Add(equipment);

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
