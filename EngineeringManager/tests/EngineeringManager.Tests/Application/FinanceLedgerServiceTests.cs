using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class FinanceLedgerServiceTests
{
    [Fact]
    public async Task CollectionsAndPaymentsCreateMatchingAccountTransactionsAndRisks()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var receivableId = await fixture.Service.AddReceivableAsync(
            new CreateReceivableRequest(
                fixture.Project.Id,
                fixture.Contract.Id,
                fixture.LegalEntity.Id,
                fixture.Partner.Id,
                ReceivableSourceType.Manual,
                new DateOnly(2026, 7, 16),
                null,
                100m,
                "进度应收"),
            CancellationToken.None);
        var payableId = await fixture.Service.AddPayableAsync(
            new CreatePayableRequest(
                fixture.Project.Id,
                fixture.Contract.Id,
                fixture.LegalEntity.Id,
                fixture.Partner.Id,
                PayableSourceType.Manual,
                new DateOnly(2026, 7, 16),
                null,
                80m,
                "班组应付"),
            CancellationToken.None);

        await fixture.Service.RecordCollectionAsync(
            new RecordCollectionRequest(
                receivableId,
                fixture.Project.Id,
                fixture.Contract.Id,
                fixture.LegalEntity.Id,
                fixture.Partner.Id,
                fixture.Bank.Id,
                new DateOnly(2026, 7, 17),
                120m,
                PaymentMethod.BankTransfer,
                "超收测试"),
            CancellationToken.None);
        await fixture.Service.RecordPaymentAsync(
            new RecordPaymentRequest(
                payableId,
                fixture.Project.Id,
                fixture.Contract.Id,
                fixture.LegalEntity.Id,
                fixture.Partner.Id,
                fixture.Bank.Id,
                new DateOnly(2026, 7, 17),
                90m,
                PaymentMethod.BankTransfer,
                "超付测试"),
            CancellationToken.None);

        var transactions = await fixture.Db.AccountTransactions.OrderBy(item => item.Direction).ToListAsync();
        transactions.Should().HaveCount(2);
        transactions.Should().ContainSingle(item =>
            item.Direction == AccountTransactionDirection.Inflow &&
            item.SourceType == AccountTransactionSourceType.Collection &&
            item.Amount == 120m);
        transactions.Should().ContainSingle(item =>
            item.Direction == AccountTransactionDirection.Outflow &&
            item.SourceType == AccountTransactionSourceType.Payment &&
            item.Amount == 90m);

        var summary = await fixture.Service.GetProjectSummaryAsync(fixture.Project.Id, CancellationToken.None);
        summary.ReceivableAmount.Should().Be(100m);
        summary.CollectedAmount.Should().Be(120m);
        summary.HasCollectionRisk.Should().BeTrue();
        summary.PayableAmount.Should().Be(80m);
        summary.PaidAmount.Should().Be(90m);
        summary.HasPaymentRisk.Should().BeTrue();
    }

    [Fact]
    public async Task RefundsDeductionsAndPaymentReversalsUpdateLedgerAndCashFlow()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var receivableId = await fixture.Service.AddReceivableAsync(
            new CreateReceivableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 16), null, 100m, null),
            CancellationToken.None);
        var collectionId = await fixture.Service.RecordCollectionAsync(
            new RecordCollectionRequest(receivableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, fixture.Bank.Id, new DateOnly(2026, 7, 17), 60m, PaymentMethod.BankTransfer, null),
            CancellationToken.None);
        var payableId = await fixture.Service.AddPayableAsync(
            new CreatePayableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, PayableSourceType.Manual, new DateOnly(2026, 7, 16), null, 80m, null),
            CancellationToken.None);
        var paymentId = await fixture.Service.RecordPaymentAsync(
            new RecordPaymentRequest(payableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, fixture.Bank.Id, new DateOnly(2026, 7, 17), 50m, PaymentMethod.BankTransfer, null),
            CancellationToken.None);

        await fixture.Service.RecordRefundAsync(
            new RecordRefundRequest(collectionId, receivableId, fixture.Bank.Id, new DateOnly(2026, 7, 18), 10m, FinancialAdjustmentType.Refund, "退回多收款"),
            CancellationToken.None);
        await fixture.Service.AddDeductionAsync(
            new CreateDeductionRequest(payableId, fixture.Project.Id, fixture.LegalEntity.Id, fixture.Partner.Id, new DateOnly(2026, 7, 18), 5m, "质量扣款"),
            CancellationToken.None);
        await fixture.Service.RecordPaymentReversalAsync(
            new RecordPaymentReversalRequest(paymentId, fixture.Bank.Id, new DateOnly(2026, 7, 18), 8m, FinancialAdjustmentType.Reversal, "付款冲销"),
            CancellationToken.None);

        var summary = await fixture.Service.GetProjectSummaryAsync(fixture.Project.Id, CancellationToken.None);
        summary.CollectedAmount.Should().Be(50m);
        summary.UncollectedAmount.Should().Be(50m);
        summary.PaidAmount.Should().Be(42m);
        summary.DeductionAmount.Should().Be(5m);
        summary.UnpaidAmount.Should().Be(33m);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.SourceType == AccountTransactionSourceType.Refund && item.Direction == AccountTransactionDirection.Outflow)).Should().Be(1);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.SourceType == AccountTransactionSourceType.PaymentReversal && item.Direction == AccountTransactionDirection.Inflow)).Should().Be(1);
    }

    [Fact]
    public async Task InternalTransferCreatesLinkedOutflowAndInflow()
    {
        await using var fixture = await FinanceFixture.CreateAsync();

        var transferId = await fixture.Service.TransferAsync(
            new CreateAccountTransferRequest(fixture.Bank.Id, fixture.Cash.Id, new DateOnly(2026, 7, 19), 20m, "备用金"),
            CancellationToken.None);

        var transfer = await fixture.Db.AccountTransfers.SingleAsync(item => item.Id == transferId);
        transfer.OutTransactionId.Should().NotBeNull();
        transfer.InTransactionId.Should().NotBeNull();
        var transactions = await fixture.Db.AccountTransactions.Where(item => item.SourceId == transferId).ToListAsync();
        transactions.Should().HaveCount(2);
        transactions.Should().ContainSingle(item => item.AccountId == fixture.Bank.Id && item.Direction == AccountTransactionDirection.Outflow && item.SourceType == AccountTransactionSourceType.TransferOut);
        transactions.Should().ContainSingle(item => item.AccountId == fixture.Cash.Id && item.Direction == AccountTransactionDirection.Inflow && item.SourceType == AccountTransactionSourceType.TransferIn);
    }

    private sealed class FinanceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private FinanceFixture(SqliteConnection connection, ApplicationDbContext db, IFinanceLedgerService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IFinanceLedgerService Service { get; }
        public LegalEntity LegalEntity { get; private set; } = null!;
        public BusinessPartner Partner { get; private set; } = null!;
        public Project Project { get; private set; } = null!;
        public Contract Contract { get; private set; } = null!;
        public FinancialAccount Bank { get; private set; } = null!;
        public FinancialAccount Cash { get; private set; } = null!;

        public static async Task<FinanceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new FinanceFixture(connection, db, new FinanceLedgerService(db));
            await fixture.SeedAsync();
            return fixture;
        }

        private async Task SeedAsync()
        {
            LegalEntity = new LegalEntity { Code = "FIN-SVC-LE", Name = "财务服务测试公司", ShortName = "测试公司" };
            Partner = new BusinessPartner { PartnerNumber = "FIN-SVC-BP", Name = "财务服务合作单位", ShortName = "合作单位" };
            Project = new Project { ProjectNumber = "FIN-SVC-P", Name = "财务服务项目", Stage = ProjectStage.UnderConstruction };
            Contract = new Contract { Project = Project, BusinessPartner = Partner, ContractNumber = "FIN-SVC-C", Name = "财务服务合同", TotalAmount = 100m };
            Project.Contracts.Add(Contract);
            Project.LegalEntities.Add(new ProjectLegalEntity { Project = Project, LegalEntity = LegalEntity, IsPrimary = true });
            Bank = new FinancialAccount { LegalEntity = LegalEntity, AccountName = "基本户", AccountType = FinancialAccountType.Bank, OpeningBalance = 1000m };
            Cash = new FinancialAccount { LegalEntity = LegalEntity, AccountName = "现金", AccountType = FinancialAccountType.Cash };
            Db.AddRange(LegalEntity, Partner, Project, Bank, Cash);
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
