using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class LegacyFinanceMigrationTests
{
    [Fact]
    public async Task MigratesLegacyHeadersOnceAndPreservesExplicitParentAllocations()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var receivable = new ReceivableEntry
        {
            Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client,
            SourceType = ReceivableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 1), Amount = 100m, Description = "旧应收"
        };
        var payable = new PayableEntry
        {
            Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Supplier,
            SourceType = PayableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 1), Amount = 80m, Description = "旧应付"
        };
        var collection = new CollectionEntry
        {
            Receivable = receivable, Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client,
            Account = fixture.CollectionAccount, CollectionDate = new DateOnly(2026, 7, 2), Amount = 40m, PaymentMethod = PaymentMethod.BankTransfer
        };
        var payment = new PaymentEntry
        {
            Payable = payable, Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Supplier,
            Account = fixture.PaymentAccount, PaymentDate = new DateOnly(2026, 7, 2), Amount = 30m, PaymentMethod = PaymentMethod.BankTransfer
        };
        var deduction = new DeductionEntry
        {
            Payable = payable, Project = fixture.Project, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Supplier,
            EntryDate = new DateOnly(2026, 7, 3), Amount = 5m, Reason = "旧扣款"
        };
        var invoice = new InvoiceEntry
        {
            Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client,
            Direction = InvoiceDirection.Output, InvoiceNumber = "LEGACY-INV-1", InvoiceDate = new DateOnly(2026, 7, 3),
            NetAmount = 60m, TaxAmount = 3.6m, GrossAmount = 63.6m, TaxRate = 0.06m, Status = InvoiceStatus.IssuedOrReceived
        };
        invoice.ReceivableLinks.Add(new InvoiceReceivableLink { Invoice = invoice, Receivable = receivable, AllocatedAmount = 63.6m });
        fixture.Db.AddRange(receivable, payable, collection, payment, deduction, invoice);
        await fixture.Db.SaveChangesAsync();

        var service = new LegacyFinanceMigrationService(fixture.Db);
        var first = await service.MigrateAsync(new LegacyFinanceMigrationOptions(), CancellationToken.None);

        first.Exceptions.Should().BeEmpty();
        (await fixture.Db.FinanceSettlements.CountAsync()).Should().Be(2);
        (await fixture.Db.FinanceCashEntries.CountAsync()).Should().Be(2);
        (await fixture.Db.FinanceInvoices.CountAsync()).Should().Be(1);
        (await fixture.Db.FinanceDeductions.CountAsync()).Should().Be(1);
        (await fixture.Db.FinanceLegacyMaps.CountAsync()).Should().Be(6);
        var centralReceivable = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Direction == LedgerDirection.Receivable);
        var centralPayable = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Direction == LedgerDirection.Payable);
        (await fixture.Db.FinanceCashEntries.Include(item => item.Allocations).SingleAsync(item => item.Direction == LedgerDirection.Receivable)).Allocations.Single().SettlementId.Should().Be(centralReceivable.Id);
        (await fixture.Db.FinanceCashEntries.Include(item => item.Allocations).SingleAsync(item => item.Direction == LedgerDirection.Payable)).Allocations.Single().SettlementId.Should().Be(centralPayable.Id);
        (await fixture.Db.FinanceInvoices.Include(item => item.Allocations).SingleAsync()).Allocations.Single().SettlementId.Should().Be(centralReceivable.Id);
        var centralDeduction = await fixture.Db.FinanceDeductions.SingleAsync();
        centralDeduction.SettlementId.Should().Be(centralPayable.Id);
        centralDeduction.ReduceInvoiceAmount.Should().BeFalse();

        var second = await service.MigrateAsync(new LegacyFinanceMigrationOptions(), CancellationToken.None);

        second.Exceptions.Should().BeEmpty();
        (await fixture.Db.FinanceSettlements.CountAsync()).Should().Be(2);
        (await fixture.Db.FinanceCashEntries.CountAsync()).Should().Be(2);
        (await fixture.Db.FinanceInvoices.CountAsync()).Should().Be(1);
        (await fixture.Db.FinanceDeductions.CountAsync()).Should().Be(1);
        (await fixture.Db.FinanceLegacyMaps.CountAsync()).Should().Be(6);
    }

    [Fact]
    public async Task ExactQuantityAndLegacyReceivableMatchRequiresManualReviewAndDoesNotBackfill()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        fixture.LineItem.IsSettlementConfirmed = true;
        fixture.LineItem.SettledQuantity = 2m;
        fixture.LineItem.SettledUnitPrice = 50m;
        var receivable = new ReceivableEntry
        {
            Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client,
            SourceType = ReceivableSourceType.StageSettlement, EntryDate = new DateOnly(2026, 7, 1), Amount = 100m
        };
        fixture.Db.Add(receivable);
        await fixture.Db.SaveChangesAsync();

        var service = new LegacyFinanceMigrationService(fixture.Db);
        var result = await service.MigrateAsync(new LegacyFinanceMigrationOptions(), CancellationToken.None);

        result.CanApply.Should().BeFalse();
        result.QuantityConflicts.Should().ContainSingle();
        var conflict = result.QuantityConflicts.Single();
        conflict.ContractLineItemId.Should().Be(fixture.LineItem.Id);
        conflict.CandidateLegacyReceivableIds.Should().ContainSingle().Which.Should().Be(receivable.Id);
        conflict.MatchReason.Should().Be("exact-amount");
        conflict.Resolution.Should().Be("manual-review");
        (await fixture.Db.FinanceSettlements.CountAsync(item => item.SourceType == LedgerSourceType.ProjectQuantity)).Should().Be(0);
    }

    [Fact]
    public async Task MigratesCashReversalsAndAccountTransferWithoutChangingOriginalHeaders()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var counterAccount = new FinancialAccount { LegalEntity = fixture.CounterLegalEntity, AccountName = "内部转入账户", AccountType = FinancialAccountType.Bank };
        var receivable = new ReceivableEntry
        {
            Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client,
            SourceType = ReceivableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 1), Amount = 100m
        };
        var collection = new CollectionEntry
        {
            Receivable = receivable, Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client,
            Account = fixture.CollectionAccount, CollectionDate = new DateOnly(2026, 7, 2), Amount = 40m
        };
        var refund = new RefundOrReversalEntry
        {
            Collection = collection, Receivable = receivable, Account = fixture.CollectionAccount, EntryDate = new DateOnly(2026, 7, 3),
            Amount = 10m, AdjustmentType = FinancialAdjustmentType.Refund, Reason = "退款"
        };
        var payable = new PayableEntry
        {
            Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Supplier,
            SourceType = PayableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 1), Amount = 80m
        };
        var payment = new PaymentEntry
        {
            Payable = payable, Project = fixture.Project, Contract = fixture.Contract, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Supplier,
            Account = fixture.PaymentAccount, PaymentDate = new DateOnly(2026, 7, 2), Amount = 30m
        };
        var reversal = new PaymentReversalEntry
        {
            Payment = payment, Account = fixture.PaymentAccount, EntryDate = new DateOnly(2026, 7, 3), Amount = 5m,
            AdjustmentType = FinancialAdjustmentType.Reversal, Reason = "付款冲销"
        };
        var transfer = new AccountTransfer
        {
            FromAccount = fixture.PaymentAccount, ToAccount = counterAccount, TransferDate = new DateOnly(2026, 7, 4), Amount = 20m, Description = "内部调拨"
        };
        fixture.Db.AddRange(counterAccount, receivable, collection, refund, payable, payment, reversal, transfer);
        await fixture.Db.SaveChangesAsync();

        var result = await new LegacyFinanceMigrationService(fixture.Db).MigrateAsync(new LegacyFinanceMigrationOptions(), CancellationToken.None);

        result.Exceptions.Should().BeEmpty();
        var cash = await fixture.Db.FinanceCashEntries.AsNoTracking().Include(item => item.Allocations).ToListAsync();
        cash.Should().HaveCount(5);
        var collectionCash = cash.Single(item => !item.IsReversal && item.CashType == LedgerCashType.Collection);
        var refundCash = cash.Single(item => item.IsReversal && item.Direction == LedgerDirection.Receivable);
        refundCash.ReversesCashEntryId.Should().Be(collectionCash.Id);
        refundCash.Amount.Should().Be(10m);
        refundCash.Allocations.Should().ContainSingle().Which.Amount.Should().Be(10m);
        var paymentCash = cash.Single(item => !item.IsReversal && item.CashType == LedgerCashType.Payment);
        var reversalCash = cash.Single(item => item.IsReversal && item.Direction == LedgerDirection.Payable);
        reversalCash.ReversesCashEntryId.Should().Be(paymentCash.Id);
        reversalCash.Amount.Should().Be(5m);
        var internalTransfer = cash.Single(item => item.CashType == LedgerCashType.InternalTransfer);
        internalTransfer.Scope.Should().Be(LedgerScope.Internal);
        internalTransfer.LegalEntityId.Should().Be(fixture.LegalEntity.Id);
        internalTransfer.CounterLegalEntityId.Should().Be(fixture.CounterLegalEntity.Id);
        internalTransfer.AccountId.Should().Be(fixture.PaymentAccount.Id);
        internalTransfer.CounterAccountId.Should().Be(counterAccount.Id);
        (await fixture.Db.RefundOrReversalEntries.CountAsync()).Should().Be(1);
        (await fixture.Db.PaymentReversalEntries.CountAsync()).Should().Be(1);
        (await fixture.Db.AccountTransfers.CountAsync()).Should().Be(1);
        (await fixture.Db.FinanceLegacyMaps.CountAsync()).Should().Be(7);
    }
}
