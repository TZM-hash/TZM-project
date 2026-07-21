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
    public async Task ProjectCollectionWithoutAnyQuantityReceivableIsRejected()
    {
        await using var fixture = await FinanceFixture.CreateAsync();

        var action = () => fixture.Service.RecordCollectionAsync(new RecordCollectionRequest(
            null, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id,
            fixture.Bank.Id, new DateOnly(2026, 7, 21), 10m, "自定义收款方式", "没有工程量应收"), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*工程量*");
        (await fixture.Db.FinanceCashEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProjectCollectionAutoAllocatesQuantityReceivablesAndKeepsCustomMethodAndExcessProjectSource()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var first = new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = LedgerSettlementState.Final,
            SourceType = LedgerSourceType.ProjectQuantity, SourceId = Guid.NewGuid(), LegalEntityId = fixture.LegalEntity.Id,
            BusinessPartnerId = fixture.Partner.Id, ProjectId = fixture.Project.Id, ContractId = fixture.Contract.Id,
            BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 30m, OriginalInvoiceAmount = 30m
        };
        var second = new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = LedgerSettlementState.Final,
            SourceType = LedgerSourceType.ProjectQuantity, SourceId = Guid.NewGuid(), LegalEntityId = fixture.LegalEntity.Id,
            BusinessPartnerId = fixture.Partner.Id, ProjectId = fixture.Project.Id, ContractId = fixture.Contract.Id,
            BusinessDate = new DateOnly(2026, 7, 2), OriginalAmount = 50m, OriginalInvoiceAmount = 50m
        };
        fixture.Db.FinanceSettlements.AddRange(first, second);
        await fixture.Db.SaveChangesAsync();

        var allocatedId = await fixture.Service.RecordCollectionAsync(new RecordCollectionRequest(
            null, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id,
            fixture.Bank.Id, new DateOnly(2026, 7, 3), 80m, "承兑汇票", null), CancellationToken.None);
        var excessId = await fixture.Service.RecordCollectionAsync(new RecordCollectionRequest(
            null, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id,
            fixture.Bank.Id, new DateOnly(2026, 7, 4), 10m, "线下核销", null), CancellationToken.None);

        var allocations = await fixture.Db.FinanceCashAllocations.AsNoTracking()
            .Where(item => item.CashEntryId == allocatedId).OrderBy(item => item.AllocationOrder).ToListAsync();
        allocations.Select(item => (item.SettlementId, item.Amount)).Should().Equal((first.Id, 30m), (second.Id, 50m));
        var allocated = await fixture.Db.FinanceCashEntries.AsNoTracking().SingleAsync(item => item.Id == allocatedId);
        allocated.PaymentMethod.Should().Be("承兑汇票");
        allocated.SourceType.Should().Be(LedgerSourceType.ProjectCollection);
        allocated.SourceId.Should().Be(fixture.Project.Id);
        var excess = await fixture.Db.FinanceCashEntries.AsNoTracking().SingleAsync(item => item.Id == excessId);
        excess.PaymentMethod.Should().Be("线下核销");
        excess.SourceType.Should().Be(LedgerSourceType.ProjectCollection);
        excess.SourceId.Should().Be(fixture.Project.Id);
        (await fixture.Db.FinanceCashAllocations.CountAsync(item => item.CashEntryId == excessId)).Should().Be(0);
    }

    [Fact]
    public async Task LegacyFinanceApiWritesOnlyCentralLedgerRecords()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var receivableId = await fixture.Service.AddReceivableAsync(
            new CreateReceivableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id,
                ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 100m, "兼容应收"),
            CancellationToken.None);
        var payableId = await fixture.Service.AddPayableAsync(
            new CreatePayableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id,
                PayableSourceType.Manual, new DateOnly(2026, 7, 1), null, 80m, "兼容应付"),
            CancellationToken.None);
        await fixture.Service.RecordCollectionAsync(
            new RecordCollectionRequest(receivableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id,
                fixture.Partner.Id, fixture.Bank.Id, new DateOnly(2026, 7, 2), 60m, PaymentMethod.BankTransfer, null),
            CancellationToken.None);
        await fixture.Service.RecordPaymentAsync(
            new RecordPaymentRequest(payableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id,
                fixture.Partner.Id, fixture.Bank.Id, new DateOnly(2026, 7, 2), 50m, PaymentMethod.BankTransfer, null),
            CancellationToken.None);

        (await fixture.Db.ReceivableEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.PayableEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.CollectionEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.PaymentEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.FinanceSettlements.CountAsync()).Should().Be(2);
        (await fixture.Db.FinanceCashEntries.CountAsync()).Should().Be(2);
        (await fixture.Db.FinanceCashAllocations.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task FinancialAccountNotesRoundTrip()
    {
        await using var fixture = await FinanceFixture.CreateAsync();

        var accountId = await fixture.Service.CreateAccountAsync(
            new CreateFinancialAccountRequest(fixture.LegalEntity.Id, "备注账户", null, null, FinancialAccountType.Bank, 0m, "账户备注"),
            CancellationToken.None);

        var account = (await fixture.Service.ListAccountsAsync(CancellationToken.None)).Single(item => item.Id == accountId);
        account.Notes.Should().Be("账户备注");
    }

    [Fact]
    public async Task SearchOverviewFiltersSortsAndPaginatesProjectSummaries()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        fixture.Project.Name = "市政一标段";
        var second = new Project { ProjectNumber = "FIN-SVC-P2", Name = "房建二标段", Stage = ProjectStage.UnderConstruction };
        second.LegalEntities.Add(new ProjectLegalEntity { Project = second, LegalEntity = fixture.LegalEntity, IsPrimary = true });
        fixture.Db.Projects.Add(second);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.AddReceivableAsync(new CreateReceivableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 300m, null), CancellationToken.None);
        await fixture.Service.AddReceivableAsync(new CreateReceivableRequest(second.Id, null, fixture.LegalEntity.Id, fixture.Partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 100m, null), CancellationToken.None);

        var result = await fixture.Service.SearchOverviewAsync(
            new FinanceOverviewQuery("标段", 150m, null, false, "UncollectedAmount", true, 1, 20),
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(item => item.ProjectId == fixture.Project.Id);
        result.Total.ReceivableAmount.Should().Be(300m);
        result.MatchingProjectIds.Should().Equal(fixture.Project.Id);
    }

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

    [Fact]
    public async Task ProjectPriorityUpdatesCanonicalFinanceRowsCashTransactionsAndAuditLogs()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var receivableId = await fixture.Service.AddReceivableAsync(new CreateReceivableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 100m, "原应收"), CancellationToken.None);
        var collectionId = await fixture.Service.RecordCollectionAsync(new RecordCollectionRequest(receivableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, fixture.Bank.Id, new DateOnly(2026, 7, 2), 40m, PaymentMethod.BankTransfer, "原收款"), CancellationToken.None);
        var payableId = await fixture.Service.AddPayableAsync(new CreatePayableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, PayableSourceType.Manual, new DateOnly(2026, 7, 3), null, 80m, "原应付"), CancellationToken.None);
        var paymentId = await fixture.Service.RecordPaymentAsync(new RecordPaymentRequest(payableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, fixture.Bank.Id, new DateOnly(2026, 7, 4), 25m, PaymentMethod.BankTransfer, "原付款"), CancellationToken.None);
        var receivable = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == receivableId);
        var collection = await fixture.Db.FinanceCashEntries.SingleAsync(item => item.Id == collectionId);
        var payable = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == payableId);
        var payment = await fixture.Db.FinanceCashEntries.SingleAsync(item => item.Id == paymentId);
        var actor = new FinanceRecordActor("project-manager", "项目经理");

        await fixture.Service.UpdateReceivableAsync(actor, new UpdateReceivableRequest(receivableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 20), 120m, "改后应收", receivable.ConcurrencyStamp, "修正应收"), CancellationToken.None);
        await fixture.Service.UpdateCollectionAsync(actor, new UpdateCollectionRequest(collectionId, receivableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, fixture.Cash.Id, new DateOnly(2026, 7, 6), 55m, PaymentMethod.Cash, "改后收款", collection.ConcurrencyStamp, "修正收款"), CancellationToken.None);
        await fixture.Service.UpdatePayableAsync(actor, new UpdatePayableRequest(payableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, new DateOnly(2026, 7, 7), new DateOnly(2026, 7, 25), 90m, "改后应付", payable.ConcurrencyStamp, "修正应付"), CancellationToken.None);
        await fixture.Service.UpdatePaymentAsync(actor, new UpdatePaymentRequest(paymentId, payableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, fixture.Cash.Id, new DateOnly(2026, 7, 8), 35m, PaymentMethod.Cash, "改后付款", payment.ConcurrencyStamp, "修正付款"), CancellationToken.None);

        (await fixture.Db.FinanceSettlements.AsNoTracking().SingleAsync(item => item.Id == receivableId)).OriginalAmount.Should().Be(120m);
        (await fixture.Db.FinanceSettlements.AsNoTracking().SingleAsync(item => item.Id == payableId)).OriginalAmount.Should().Be(90m);
        var collectionTransaction = await fixture.Db.AccountTransactions.AsNoTracking().SingleAsync(item => item.SourceType == AccountTransactionSourceType.Collection && item.SourceId == collectionId);
        collectionTransaction.AccountId.Should().Be(fixture.Cash.Id);
        collectionTransaction.TransactionDate.Should().Be(new DateOnly(2026, 7, 6));
        collectionTransaction.Amount.Should().Be(55m);
        var paymentTransaction = await fixture.Db.AccountTransactions.AsNoTracking().SingleAsync(item => item.SourceType == AccountTransactionSourceType.Payment && item.SourceId == paymentId);
        paymentTransaction.AccountId.Should().Be(fixture.Cash.Id);
        paymentTransaction.Amount.Should().Be(35m);
        var audits = await fixture.Db.AuditLogs.AsNoTracking().Where(item => item.UserId == actor.UserId && item.Action.StartsWith("Update")).ToListAsync();
        audits.Should().HaveCount(4);
        audits.Should().OnlyContain(item => item.BeforeJson != null && item.AfterJson != null && item.RelatedProjectId == fixture.Project.Id.ToString());
    }

    [Fact]
    public async Task UpdatingInvoiceRescalesExistingAllocationsAndRejectsStaleVersion()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var receivableId = await fixture.Service.AddReceivableAsync(new CreateReceivableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 200m, null), CancellationToken.None);
        var invoiceId = await fixture.Service.AddInvoiceAsync(new CreateInvoiceRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, InvoiceDirection.Output, "INV-UP-01", new DateOnly(2026, 7, 2), fixture.TaxConfiguration.Id, 100m, 0m, 100m, InvoiceStatus.IssuedOrReceived, [new InvoiceAllocationRequest(receivableId, 100m)], []), CancellationToken.None);
        var invoice = await fixture.Db.FinanceInvoices.SingleAsync(item => item.Id == invoiceId);
        var originalStamp = invoice.ConcurrencyStamp;
        var actor = new FinanceRecordActor("project-manager", "项目经理");

        await fixture.Service.UpdateInvoiceAsync(actor, new UpdateInvoiceRequest(invoiceId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, InvoiceDirection.Output, "INV-UP-01", new DateOnly(2026, 7, 3), fixture.TaxConfiguration.Id, 60m, 0m, 60m, InvoiceStatus.IssuedOrReceived, originalStamp, "修正开票金额"), CancellationToken.None);

        (await fixture.Db.FinanceInvoiceAllocations.AsNoTracking().SingleAsync(item => item.InvoiceId == invoiceId)).Amount.Should().Be(60m);
        var staleAction = () => fixture.Service.UpdateInvoiceAsync(actor, new UpdateInvoiceRequest(invoiceId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, InvoiceDirection.Output, "INV-UP-01", new DateOnly(2026, 7, 3), fixture.TaxConfiguration.Id, 50m, 0m, 50m, InvoiceStatus.IssuedOrReceived, originalStamp, "过期修改"), CancellationToken.None);
        await staleAction.Should().ThrowAsync<DbUpdateConcurrencyException>().WithMessage("*刷新后重试*");
    }

    [Fact]
    public async Task InvoiceUsesEnabledProjectTaxConfiguration()
    {
        await using var fixture = await FinanceFixture.CreateAsync();

        var invoiceId = await fixture.Service.AddInvoiceAsync(
            new CreateInvoiceRequest(
                fixture.Project.Id,
                fixture.Contract.Id,
                fixture.LegalEntity.Id,
                fixture.Partner.Id,
                InvoiceDirection.Output,
                "INV-TAX-01",
                new DateOnly(2026, 7, 18),
                fixture.TaxConfiguration.Id,
                100m,
                3m,
                103m,
                InvoiceStatus.IssuedOrReceived,
                [],
                []),
            CancellationToken.None);

        var invoice = await fixture.Db.FinanceInvoices.SingleAsync(item => item.Id == invoiceId);
        invoice.ProjectTaxConfigurationId.Should().Be(fixture.TaxConfiguration.Id);
        invoice.TaxRate.Should().Be(0.03m);
        invoice.InvoiceType.Should().Be("专票");
    }

    [Fact]
    public async Task InvoiceRejectsTaxConfigurationFromAnotherProjectOrDisabledConfiguration()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var otherProject = new Project { ProjectNumber = "FIN-TAX-OTHER", Name = "其他税金项目" };
        var otherConfiguration = new ProjectTaxConfiguration
        {
            Project = otherProject,
            TaxRate = 0.09m,
            InvoiceType = ProjectInvoiceType.Ordinary
        };
        fixture.Db.Add(otherConfiguration);
        await fixture.Db.SaveChangesAsync();

        var otherProjectAction = () => fixture.Service.AddInvoiceAsync(
            new CreateInvoiceRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id,
                InvoiceDirection.Output, "INV-TAX-OTHER", new DateOnly(2026, 7, 18), otherConfiguration.Id,
                100m, 9m, 109m, InvoiceStatus.IssuedOrReceived, [], []),
            CancellationToken.None);
        await otherProjectAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*税金配置*");

        fixture.TaxConfiguration.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        var disabledAction = () => fixture.Service.AddInvoiceAsync(
            new CreateInvoiceRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id,
                InvoiceDirection.Output, "INV-TAX-DISABLED", new DateOnly(2026, 7, 18), fixture.TaxConfiguration.Id,
                100m, 3m, 103m, InvoiceStatus.IssuedOrReceived, [], []),
            CancellationToken.None);
        await disabledAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*税金配置*");
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
        public ProjectTaxConfiguration TaxConfiguration { get; private set; } = null!;
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
            TaxConfiguration = new ProjectTaxConfiguration { Project = Project, TaxRate = 0.03m, InvoiceType = ProjectInvoiceType.Special };
            Project.TaxConfigurations.Add(TaxConfiguration);
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
