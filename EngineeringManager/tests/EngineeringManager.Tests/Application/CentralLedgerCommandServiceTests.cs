using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class CentralLedgerCommandServiceTests
{
    [Fact]
    public async Task ApplicationContractsExposeConfirmedMultiEntryShape()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var actor = fixture.ExternalActor();
        var request = new CreateSettlementRequest(
            LedgerScope.External,
            LedgerDirection.Receivable,
            LedgerSettlementState.Final,
            LedgerSourceType.ProjectQuantity,
            fixture.LineItem.Id,
            fixture.LegalEntity.Id,
            fixture.Client.Id,
            null,
            fixture.Project.Id,
            fixture.Contract.Id,
            fixture.LineItem.Id,
            new DateOnly(2026, 7, 19),
            1_000_000m,
            1_000_000m,
            "工程量确认形成正式应收");
        var deduction = new AddFinanceDeductionRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 7, 20),
            10_000m,
            true,
            "扣减实际应收并扣减应开票",
            Guid.NewGuid());
        var allocation = new FinanceAllocationRequest(Guid.NewGuid(), 500_000m, 1);

        actor.CanManageExternal.Should().BeTrue();
        request.BusinessPartnerId.Should().Be(fixture.Client.Id);
        request.CounterLegalEntityId.Should().BeNull();
        request.SettlementState.Should().Be(LedgerSettlementState.Final);
        deduction.ReduceInvoiceAmount.Should().BeTrue();
        allocation.Amount.Should().Be(500_000m);
        typeof(ICentralLedgerCommandService).GetMethods().Select(method => method.Name).Should().Contain(
        [
            nameof(ICentralLedgerCommandService.CreateSettlementAsync),
            nameof(ICentralLedgerCommandService.FinalizeSettlementAsync),
            nameof(ICentralLedgerCommandService.AddDeductionAsync),
            nameof(ICentralLedgerCommandService.CreateInvoiceAsync),
            nameof(ICentralLedgerCommandService.CreateCashAsync),
            nameof(ICentralLedgerCommandService.ReplaceInvoiceAllocationsAsync),
            nameof(ICentralLedgerCommandService.ReplaceCashAllocationsAsync),
            nameof(ICentralLedgerCommandService.DeleteAsync)
        ]);
    }

    [Fact]
    public async Task CreateProjectReceivableIsFormalImmediately()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);

        var id = await service.CreateSettlementAsync(
            fixture.ExternalActor(),
            CreateSettlementRequest(fixture, LedgerDirection.Receivable, LedgerSettlementState.Final, 1_000m),
            CancellationToken.None);

        var saved = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == id);
        saved.SettlementState.Should().Be(LedgerSettlementState.Final);
        saved.SourceType.Should().Be(LedgerSourceType.ProjectQuantity);
        saved.OriginalAmount.Should().Be(1_000m);
    }

    [Fact]
    public async Task FinalizingProvisionalSettlementAddsTraceableDelta()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var id = await service.CreateSettlementAsync(
            fixture.ExternalActor(),
            CreateSettlementRequest(fixture, LedgerDirection.Receivable, LedgerSettlementState.Provisional, 800m),
            CancellationToken.None);
        var stamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == id)).ConcurrencyStamp;

        await service.FinalizeSettlementAsync(
            fixture.ExternalActor(),
            new FinalizeSettlementRequest(id, new DateOnly(2026, 7, 20), 1_000m, 900m, "确认最终结算", stamp),
            CancellationToken.None);

        var saved = await fixture.Db.FinanceSettlements.Include(item => item.Adjustments).SingleAsync(item => item.Id == id);
        saved.SettlementState.Should().Be(LedgerSettlementState.Final);
        saved.OriginalAmount.Should().Be(800m);
        saved.Adjustments.Single().AmountDelta.Should().Be(200m);
        saved.Adjustments.Single().InvoiceAmountDelta.Should().Be(100m);
        saved.Adjustments.Single().Reason.Should().Be("确认最终结算");
    }

    [Fact]
    public async Task DeductionAlwaysReducesActualAmount()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var id = await CreateSettlementAsync(service, fixture, LedgerDirection.Payable, 1_000m);
        var stamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == id)).ConcurrencyStamp;

        await service.AddDeductionAsync(
            fixture.ExternalActor(),
            new AddFinanceDeductionRequest(id, new DateOnly(2026, 7, 20), 100m, false, "只扣应付", stamp),
            CancellationToken.None);

        var metrics = await CalculateAsync(fixture.Db, id);
        metrics.ActualAmount.Should().Be(900m);
        metrics.ShouldInvoiceAmount.Should().Be(1_000m);
        metrics.CashAmount.Should().Be(0m);
    }

    [Fact]
    public async Task DeductionOptionControlsInvoiceReduction()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var id = await CreateSettlementAsync(service, fixture, LedgerDirection.Payable, 1_000m);
        var stamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == id)).ConcurrencyStamp;

        await service.AddDeductionAsync(
            fixture.ExternalActor(),
            new AddFinanceDeductionRequest(id, new DateOnly(2026, 7, 20), 100m, true, "同时扣应开票", stamp),
            CancellationToken.None);

        var metrics = await CalculateAsync(fixture.Db, id);
        metrics.ActualAmount.Should().Be(900m);
        metrics.ShouldInvoiceAmount.Should().Be(900m);
    }

    [Fact]
    public async Task CrewPaymentDeductionIsNotCashPaid()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var id = await CreateSettlementAsync(service, fixture, LedgerDirection.Payable, 1_000m, fixture.Crew.Id);
        var stamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == id)).ConcurrencyStamp;
        await service.AddDeductionAsync(
            fixture.ExternalActor(),
            new AddFinanceDeductionRequest(id, new DateOnly(2026, 7, 20), 100m, false, "班组质量扣款", stamp),
            CancellationToken.None);
        await service.CreateCashAsync(
            fixture.ExternalActor(),
            new CreateFinanceCashRequest(
                LedgerScope.External,
                LedgerDirection.Payable,
                LedgerCashType.Payment,
                LedgerSourceType.Crew,
                null,
                fixture.LegalEntity.Id,
                fixture.Crew.Id,
                null,
                fixture.PaymentAccount.Id,
                null,
                new DateOnly(2026, 7, 21),
                400m,
                "银行转账",
                null,
                [new FinanceAllocationRequest(id, 400m, 1)]),
            CancellationToken.None);

        var metrics = await CalculateAsync(fixture.Db, id);
        metrics.Deductions.Should().Be(100m);
        metrics.CashAmount.Should().Be(400m);
        metrics.UncollectedOrUnpaid.Should().Be(500m);
    }

    [Fact]
    public async Task DeletingSettlementDetachesAllocationsAndLeavesHeaders()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var settlementId = await CreateSettlementAsync(service, fixture, LedgerDirection.Receivable, 1_000m);
        var invoiceId = await service.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "OUT-DELETE-001",
                new DateOnly(2026, 7, 20),
                600m,
                null,
                null,
                null,
                null,
                [new FinanceAllocationRequest(settlementId, 600m, 1)]),
            CancellationToken.None);
        var cashId = await service.CreateCashAsync(
            fixture.ExternalActor(),
            new CreateFinanceCashRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerCashType.Collection,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                fixture.CollectionAccount.Id,
                null,
                new DateOnly(2026, 7, 21),
                500m,
                "银行转账",
                null,
                [new FinanceAllocationRequest(settlementId, 500m, 1)]),
            CancellationToken.None);
        var stamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == settlementId)).ConcurrencyStamp;

        await service.DeleteAsync(
            fixture.ExternalActor(),
            new DeleteFinanceRecordRequest(FinanceRecordType.Settlement, settlementId, stamp, "错误结算单", "中央账本"),
            CancellationToken.None);

        (await fixture.Db.FinanceSettlements.AnyAsync(item => item.Id == settlementId)).Should().BeFalse();
        (await fixture.Db.FinanceInvoices.AnyAsync(item => item.Id == invoiceId)).Should().BeTrue();
        (await fixture.Db.FinanceCashEntries.AnyAsync(item => item.Id == cashId)).Should().BeTrue();
        (await fixture.Db.FinanceInvoiceAllocations.AnyAsync(item => item.SettlementId == settlementId)).Should().BeFalse();
        (await fixture.Db.FinanceCashAllocations.AnyAsync(item => item.SettlementId == settlementId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingDeductionRestoresActualAndInvoiceMetrics()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var settlementId = await CreateSettlementAsync(service, fixture, LedgerDirection.Payable, 1_000m);
        var settlementStamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == settlementId)).ConcurrencyStamp;
        var deductionId = await service.AddDeductionAsync(
            fixture.ExternalActor(),
            new AddFinanceDeductionRequest(settlementId, new DateOnly(2026, 7, 20), 100m, true, "错误扣款", settlementStamp),
            CancellationToken.None);
        var deductionStamp = (await fixture.Db.FinanceDeductions.SingleAsync(item => item.Id == deductionId)).ConcurrencyStamp;

        await service.DeleteAsync(
            fixture.ExternalActor(),
            new DeleteFinanceRecordRequest(FinanceRecordType.Deduction, deductionId, deductionStamp, "撤销错误扣款", "班组管理"),
            CancellationToken.None);

        var metrics = await CalculateAsync(fixture.Db, settlementId);
        metrics.ActualAmount.Should().Be(1_000m);
        metrics.ShouldInvoiceAmount.Should().Be(1_000m);
    }

    [Fact]
    public async Task DeleteRequiresReasonAndWritesImmutableSnapshot()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var settlementId = await CreateSettlementAsync(service, fixture, LedgerDirection.Payable, 1_000m);
        var stamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == settlementId)).ConcurrencyStamp;

        var invalid = () => service.DeleteAsync(
            fixture.ExternalActor(),
            new DeleteFinanceRecordRequest(FinanceRecordType.Settlement, settlementId, stamp, " ", "中央账本"),
            CancellationToken.None);

        await invalid.Should().ThrowAsync<ArgumentException>().WithMessage("*删除原因*");
        await service.DeleteAsync(
            fixture.ExternalActor(),
            new DeleteFinanceRecordRequest(FinanceRecordType.Settlement, settlementId, stamp, "重复录入", "中央账本"),
            CancellationToken.None);

        var log = await fixture.Db.FinanceDeletionLogs.SingleAsync();
        log.RecordId.Should().Be(settlementId);
        log.Reason.Should().Be("重复录入");
        log.SnapshotJson.Should().Contain("OriginalAmount");
        log.BeforeMetricsJson.Should().Contain("ActualAmount");
        (await fixture.Db.AuditLogs.AnyAsync(item => item.EntityId == settlementId.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task StaleConcurrencyStampRejectsFinalizationAndDelete()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var id = await service.CreateSettlementAsync(
            fixture.ExternalActor(),
            CreateSettlementRequest(fixture, LedgerDirection.Receivable, LedgerSettlementState.Provisional, 800m),
            CancellationToken.None);
        var staleStamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == id)).ConcurrencyStamp;
        await service.FinalizeSettlementAsync(
            fixture.ExternalActor(),
            new FinalizeSettlementRequest(id, new DateOnly(2026, 7, 20), 900m, 900m, "首次确认", staleStamp),
            CancellationToken.None);

        var staleFinalize = () => service.FinalizeSettlementAsync(
            fixture.ExternalActor(),
            new FinalizeSettlementRequest(id, new DateOnly(2026, 7, 21), 1_000m, 1_000m, "并发覆盖", staleStamp),
            CancellationToken.None);
        var staleDelete = () => service.DeleteAsync(
            fixture.ExternalActor(),
            new DeleteFinanceRecordRequest(FinanceRecordType.Settlement, id, staleStamp, "并发删除", "中央账本"),
            CancellationToken.None);

        await staleFinalize.Should().ThrowAsync<DbUpdateConcurrencyException>().WithMessage("*刷新后重试*");
        await staleDelete.Should().ThrowAsync<DbUpdateConcurrencyException>().WithMessage("*刷新后重试*");
    }

    private static CreateSettlementRequest CreateSettlementRequest(
        CentralLedgerTestFixture fixture,
        LedgerDirection direction,
        LedgerSettlementState state,
        decimal amount,
        Guid? businessPartnerId = null)
    {
        return new CreateSettlementRequest(
            LedgerScope.External,
            direction,
            state,
            direction == LedgerDirection.Receivable ? LedgerSourceType.ProjectQuantity : LedgerSourceType.Partner,
            direction == LedgerDirection.Receivable ? fixture.LineItem.Id : null,
            fixture.LegalEntity.Id,
            businessPartnerId ?? (direction == LedgerDirection.Receivable ? fixture.Client.Id : fixture.Supplier.Id),
            null,
            fixture.Project.Id,
            fixture.Contract.Id,
            direction == LedgerDirection.Receivable ? fixture.LineItem.Id : null,
            new DateOnly(2026, 7, 19),
            amount,
            amount,
            null);
    }

    private static Task<Guid> CreateSettlementAsync(
        CentralLedgerCommandService service,
        CentralLedgerTestFixture fixture,
        LedgerDirection direction,
        decimal amount,
        Guid? businessPartnerId = null)
    {
        return service.CreateSettlementAsync(
            fixture.ExternalActor(),
            CreateSettlementRequest(fixture, direction, LedgerSettlementState.Final, amount, businessPartnerId),
            CancellationToken.None);
    }

    private static async Task<CentralLedgerMetrics> CalculateAsync(ApplicationDbContext db, Guid settlementId)
    {
        var settlement = await db.FinanceSettlements.AsNoTracking().SingleAsync(item => item.Id == settlementId);
        var adjustments = await db.FinanceSettlementAdjustments.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.Status == LedgerRecordStatus.Active)
            .ToListAsync();
        var deductions = await db.FinanceDeductions.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.Status == LedgerRecordStatus.Active)
            .ToListAsync();
        var invoiced = await db.FinanceInvoiceAllocations.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.Invoice.Status == LedgerRecordStatus.Active)
            .SumAsync(item => (decimal?)item.Amount) ?? 0m;
        var cash = await db.FinanceCashAllocations.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.CashEntry.Status == LedgerRecordStatus.Active)
            .SumAsync(item => (decimal?)(item.CashEntry.IsReversal ? -item.Amount : item.Amount)) ?? 0m;
        return CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(
            settlement.OriginalAmount + adjustments.Sum(item => item.AmountDelta),
            deductions.Sum(item => item.Amount),
            deductions.Where(item => item.ReduceInvoiceAmount).Sum(item => item.Amount),
            settlement.OriginalInvoiceAmount + adjustments.Sum(item => item.InvoiceAmountDelta),
            invoiced,
            cash));
    }
}
