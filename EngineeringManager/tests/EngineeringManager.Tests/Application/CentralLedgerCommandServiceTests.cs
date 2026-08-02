using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
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
        var settlementId = await CreateSettlementAsync(service, fixture, LedgerDirection.Receivable, 1_000m, sourceType: LedgerSourceType.CentralLedger);
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
    public async Task UpdatingCentralLedgerCashKeepsAccountTransactionProjectionInSync()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var actor = fixture.ExternalActor();
        var cashId = await service.CreateCashAsync(
            actor,
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
                new DateOnly(2026, 7, 1),
                100m,
                "银行转账",
                "原收款",
                []),
            CancellationToken.None);
        var stamp = (await fixture.Db.FinanceCashEntries.SingleAsync(item => item.Id == cashId)).ConcurrencyStamp;

        await service.UpdateCashAsync(
            actor,
            new UpdateFinanceCashRequest(
                cashId,
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerCashType.Collection,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                null,
                null,
                fixture.PaymentAccount.Id,
                null,
                new DateOnly(2026, 7, 2),
                250m,
                "现金",
                "更新收款",
                "更正收款信息",
                stamp),
            CancellationToken.None);

        var transaction = await fixture.Db.AccountTransactions.SingleAsync(item => item.SourceId == cashId);
        transaction.AccountId.Should().Be(fixture.PaymentAccount.Id);
        transaction.TransactionDate.Should().Be(new DateOnly(2026, 7, 2));
        transaction.Amount.Should().Be(250m);
        transaction.Description.Should().Be("更新收款");
        transaction.SourceType.Should().Be(AccountTransactionSourceType.Collection);
        transaction.Direction.Should().Be(AccountTransactionDirection.Inflow);
    }

    [Fact]
    public async Task DeletingCentralLedgerCashRemovesAccountTransactionProjection()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var cashId = await service.CreateCashAsync(
            fixture.ExternalActor(),
            new CreateFinanceCashRequest(
                LedgerScope.External,
                LedgerDirection.Payable,
                LedgerCashType.Payment,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Supplier.Id,
                null,
                fixture.PaymentAccount.Id,
                null,
                new DateOnly(2026, 7, 3),
                125m,
                "银行转账",
                "付款",
                []),
            CancellationToken.None);
        var stamp = (await fixture.Db.FinanceCashEntries.SingleAsync(item => item.Id == cashId)).ConcurrencyStamp;

        await service.DeleteAsync(
            fixture.ExternalActor(),
            new DeleteFinanceRecordRequest(FinanceRecordType.Cash, cashId, stamp, "删除重复付款", "中央账本"),
            CancellationToken.None);

        (await fixture.Db.FinanceCashEntries.AnyAsync(item => item.Id == cashId)).Should().BeFalse();
        (await fixture.Db.AccountTransactions.AnyAsync(item => item.SourceId == cashId)).Should().BeFalse();
    }

    [Fact]
    public async Task InternalTransferProjectsBothAccountMovementsFromOneCentralCashRecord()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var counterAccount = new FinancialAccount
        {
            LegalEntity = fixture.CounterLegalEntity,
            AccountName = "对方账户",
            AccountType = FinancialAccountType.Bank
        };
        fixture.Db.Add(counterAccount);
        await fixture.Db.SaveChangesAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var actor = fixture.InternalActor();
        var cashId = await service.CreateCashAsync(
            actor,
            new CreateFinanceCashRequest(
                LedgerScope.Internal,
                LedgerDirection.Payable,
                LedgerCashType.InternalTransfer,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                null,
                fixture.CounterLegalEntity.Id,
                fixture.PaymentAccount.Id,
                counterAccount.Id,
                new DateOnly(2026, 7, 4),
                500m,
                "内部转账",
                "公司间调拨",
                []),
            CancellationToken.None);

        var transactions = await fixture.Db.AccountTransactions.Where(item => item.SourceId == cashId).ToListAsync();
        transactions.Should().HaveCount(2);
        transactions.Should().ContainSingle(item => item.AccountId == fixture.PaymentAccount.Id && item.Direction == AccountTransactionDirection.Outflow && item.SourceType == AccountTransactionSourceType.TransferOut && item.Amount == 500m);
        transactions.Should().ContainSingle(item => item.AccountId == counterAccount.Id && item.Direction == AccountTransactionDirection.Inflow && item.SourceType == AccountTransactionSourceType.TransferIn && item.Amount == 500m);
    }

    [Fact]
    public async Task CreatingCashValidatesProjectAndContractRelationship()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);

        var action = () => service.CreateCashAsync(
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
                new DateOnly(2026, 7, 5),
                100m,
                "银行转账",
                "项目收款",
                [],
                ProjectId: fixture.Project.Id,
                ContractId: Guid.NewGuid()),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*合同不属于所选项目*");
    }

    [Fact]
    public async Task CreatingInvoiceValidatesProjectAndContractRelationship()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);

        var action = () => service.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "OUT-CONTEXT-001",
                new DateOnly(2026, 7, 5),
                100m,
                null,
                null,
                null,
                "项目发票",
                [],
                ProjectId: fixture.Project.Id,
                ContractId: Guid.NewGuid()),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*合同不属于所选项目*");
    }

    [Fact]
    public async Task CreatingCashRejectsDirectionAndCashTypeMismatch()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);

        var action = () => service.CreateCashAsync(
            fixture.ExternalActor(),
            new CreateFinanceCashRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerCashType.Payment,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                fixture.CollectionAccount.Id,
                null,
                new DateOnly(2026, 7, 5),
                100m,
                "银行转账",
                "错误方向",
                []),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*外部账本收款必须使用 Collection，付款必须使用 Payment*");
    }

    [Fact]
    public async Task CreatingInvoiceRejectsDuplicateNumberWithinLegalEntityAndDirection()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var request = new CreateFinanceInvoiceRequest(
            LedgerScope.External,
            LedgerDirection.Receivable,
            LedgerSourceType.CentralLedger,
            null,
            fixture.LegalEntity.Id,
            fixture.Client.Id,
            null,
            "OUT-DUPLICATE-001",
            new DateOnly(2026, 7, 5),
            100m,
            null,
            null,
            null,
            "首张发票",
            []);

        await service.CreateInvoiceAsync(fixture.ExternalActor(), request, CancellationToken.None);

        var action = () => service.CreateInvoiceAsync(
            fixture.ExternalActor(),
            request with { InvoiceDate = new DateOnly(2026, 7, 6), Notes = "重复发票" },
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*发票号码已存在*");
    }

    [Fact]
    public async Task CreatingInvoiceRejectsUndefinedStatus()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var action = () => service.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "OUT-INVALID-STATUS",
                new DateOnly(2026, 7, 5),
                100m,
                null,
                null,
                null,
                null,
                [],
                Status: (LedgerRecordStatus)999),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*发票状态无效*");
        (await fixture.Db.FinanceInvoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreatingLedgerRecordRejectsLegalEntityNotLinkedToProject()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var unrelatedLegalEntity = new LegalEntity { Code = "UNLINKED", Name = "未关联公司" };
        var unrelatedAccount = new FinancialAccount
        {
            LegalEntity = unrelatedLegalEntity,
            AccountName = "未关联账户",
            AccountType = FinancialAccountType.Bank
        };
        fixture.Db.AddRange(unrelatedLegalEntity, unrelatedAccount);
        await fixture.Db.SaveChangesAsync();
        var actor = new CentralLedgerActor(
            "external-user",
            "外部账用户",
            new HashSet<Guid> { fixture.LegalEntity.Id, unrelatedLegalEntity.Id },
            new HashSet<Guid> { fixture.Project.Id },
            true,
            false,
            false,
            false);

        var action = () => new CentralLedgerCommandService(fixture.Db).CreateCashAsync(
            actor,
            new CreateFinanceCashRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerCashType.Collection,
                LedgerSourceType.CentralLedger,
                null,
                unrelatedLegalEntity.Id,
                fixture.Client.Id,
                null,
                unrelatedAccount.Id,
                null,
                new DateOnly(2026, 7, 7),
                100m,
                "银行转账",
                "未关联项目公司",
                [],
                ProjectId: fixture.Project.Id),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*签约公司*");
    }

    [Fact]
    public async Task CreatingSettlementRejectsInactiveContract()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        fixture.Contract.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var action = () => new CentralLedgerCommandService(fixture.Db).CreateSettlementAsync(
            fixture.ExternalActor(),
            CreateSettlementRequest(fixture, LedgerDirection.Receivable, LedgerSettlementState.Final, 100m),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*已停用*");
    }

    [Fact]
    public async Task AutomaticAllocationHonorsInvoiceProjectScope()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var sameProject = await CreateSettlementAsync(command, fixture, LedgerDirection.Receivable, 200m);
        var (otherProject, otherContract) = await AddProjectAsync(fixture, "AUTO-SCOPE");
        var otherProjectSettlement = await command.CreateSettlementAsync(
            fixture.ExternalActor(),
            new CreateSettlementRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSettlementState.Final,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                otherProject.Id,
                otherContract.Id,
                null,
                new DateOnly(2026, 7, 2),
                900m,
                900m,
                null),
            CancellationToken.None);

        var invoiceId = await command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "SCOPED-AUTO-001",
                new DateOnly(2026, 7, 3),
                500m,
                null,
                null,
                null,
                null,
                [],
                AutoAllocate: true,
                ProjectId: fixture.Project.Id),
            CancellationToken.None);

        var allocations = await fixture.Db.FinanceInvoiceAllocations.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId)
            .ToListAsync();
        allocations.Should().ContainSingle().Which.SettlementId.Should().Be(sameProject);
        allocations.Should().NotContain(item => item.SettlementId == otherProjectSettlement);
    }

    [Fact]
    public async Task ManualAllocationMustMatchInvoiceProjectAndCannotRepeatSettlement()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var settlementId = await CreateSettlementAsync(command, fixture, LedgerDirection.Receivable, 500m);

        var wrongProject = await AddProjectAsync(fixture, "MANUAL-SCOPE");
        var wrongProjectSettlement = await command.CreateSettlementAsync(
            fixture.ExternalActor(),
            new CreateSettlementRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSettlementState.Final,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                wrongProject.Project.Id,
                wrongProject.Contract.Id,
                null,
                new DateOnly(2026, 7, 2),
                500m,
                500m,
                null),
            CancellationToken.None);

        var wrongProjectAction = () => command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "MANUAL-SCOPE-001",
                new DateOnly(2026, 7, 3),
                100m,
                null,
                null,
                null,
                null,
                [new FinanceAllocationRequest(wrongProjectSettlement, 100m, 1)],
                ProjectId: fixture.Project.Id),
            CancellationToken.None);
        await wrongProjectAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*项目*");

        var duplicateAction = () => command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "MANUAL-SCOPE-002",
                new DateOnly(2026, 7, 3),
                200m,
                null,
                null,
                null,
                null,
                [new FinanceAllocationRequest(settlementId, 100m, 1), new FinanceAllocationRequest(settlementId, 100m, 2)],
                ProjectId: fixture.Project.Id),
            CancellationToken.None);
        await duplicateAction.Should().ThrowAsync<ArgumentException>().WithMessage("*分摊目标不能重复*");
    }

    [Fact]
    public async Task CreatingInvoiceValidatesTaxConfigurationAndAmounts()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var otherProject = new Project { ProjectNumber = "TAX-OTHER", Name = "税务配置其他项目", Stage = ProjectStage.UnderConstruction };
        var foreignTax = new ProjectTaxConfiguration { Project = otherProject, TaxRate = 0.06m, InvoiceType = ProjectInvoiceType.Ordinary };
        fixture.Db.AddRange(otherProject, foreignTax);
        await fixture.Db.SaveChangesAsync();

        var foreignTaxAction = () => new CentralLedgerCommandService(fixture.Db).CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "TAX-CONFIG-001",
                new DateOnly(2026, 7, 3),
                106m,
                100m,
                6m,
                0.06m,
                null,
                [],
                ProjectTaxConfigurationId: foreignTax.Id,
                ProjectId: fixture.Project.Id),
            CancellationToken.None);
        await foreignTaxAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*税务配置*");

        var amountAction = () => new CentralLedgerCommandService(fixture.Db).CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "TAX-CONFIG-002",
                new DateOnly(2026, 7, 3),
                106m,
                100m,
                5m,
                0.06m,
                null,
                [],
                ProjectId: fixture.Project.Id),
            CancellationToken.None);
        await amountAction.Should().ThrowAsync<ArgumentException>().WithMessage("*不含税金额加税额*");
    }

    [Fact]
    public async Task DeletingSourceSettlementIsRejectedByCentralLedger()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var settlementId = await service.CreateSettlementAsync(
            fixture.ExternalActor(),
            new CreateSettlementRequest(
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
                new DateOnly(2026, 7, 3),
                100m,
                100m,
                null),
            CancellationToken.None);
        var stamp = (await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == settlementId)).ConcurrencyStamp;

        var action = () => service.DeleteAsync(
            fixture.ExternalActor(),
            new DeleteFinanceRecordRequest(FinanceRecordType.Settlement, settlementId, stamp, "误删来源记录", "中央账本"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*来源模块*");
    }

    [Fact]
    public async Task CentralCashProjectionDoesNotDeleteOtherAccountTransactionSources()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var entryId = Guid.NewGuid();
        var unrelated = new AccountTransaction
        {
            Account = fixture.CollectionAccount,
            Direction = AccountTransactionDirection.Outflow,
            SourceType = AccountTransactionSourceType.PayrollPayment,
            SourceId = entryId,
            TransactionDate = new DateOnly(2026, 7, 1),
            Amount = 999m,
            Description = "其他模块流水"
        };
        fixture.Db.AccountTransactions.Add(unrelated);
        await fixture.Db.SaveChangesAsync();

        var cashId = await new CentralLedgerCommandService(fixture.Db).CreateCashAsync(
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
                new DateOnly(2026, 7, 2),
                100m,
                "银行转账",
                "中央流水",
                [],
                EntryId: entryId),
            CancellationToken.None);
        var stamp = (await fixture.Db.FinanceCashEntries.SingleAsync(item => item.Id == cashId)).ConcurrencyStamp;

        await new CentralLedgerCommandService(fixture.Db).DeleteAsync(
            fixture.ExternalActor(),
            new DeleteFinanceRecordRequest(FinanceRecordType.Cash, cashId, stamp, "删除测试流水", "中央账本"),
            CancellationToken.None);

        (await fixture.Db.AccountTransactions.AnyAsync(item => item.Id == unrelated.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task UpdatingInvoiceRejectsDuplicateNumberWithinLegalEntityAndDirection()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var firstId = await service.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "OUT-DUPLICATE-002",
                new DateOnly(2026, 7, 5),
                100m,
                null,
                null,
                null,
                null,
                []),
            CancellationToken.None);
        var secondId = await service.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "OUT-DUPLICATE-003",
                new DateOnly(2026, 7, 5),
                100m,
                null,
                null,
                null,
                null,
                []),
            CancellationToken.None);
        var stamp = (await fixture.Db.FinanceInvoices.SingleAsync(item => item.Id == secondId)).ConcurrencyStamp;

        var action = () => service.UpdateInvoiceAsync(
            fixture.ExternalActor(),
            new UpdateFinanceInvoiceRequest(
                secondId,
                LedgerScope.External,
                LedgerDirection.Receivable,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                null,
                null,
                "OUT-DUPLICATE-002",
                new DateOnly(2026, 7, 6),
                100m,
                null,
                null,
                null,
                null,
                null,
                "改为重复号码",
                stamp),
            CancellationToken.None);

        firstId.Should().NotBe(secondId);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*发票号码已存在*");
    }

    [Fact]
    public async Task DeletingDeductionRestoresActualAndInvoiceMetrics()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new CentralLedgerCommandService(fixture.Db);
        var settlementId = await CreateSettlementAsync(service, fixture, LedgerDirection.Payable, 1_000m, sourceType: LedgerSourceType.CentralLedger);
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
        var settlementId = await CreateSettlementAsync(service, fixture, LedgerDirection.Payable, 1_000m, sourceType: LedgerSourceType.CentralLedger);
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
        Guid? businessPartnerId = null,
        LedgerSourceType? sourceType = null)
    {
        return service.CreateSettlementAsync(
            fixture.ExternalActor(),
            sourceType.HasValue
                ? CreateSettlementRequest(fixture, direction, LedgerSettlementState.Final, amount, businessPartnerId) with
                {
                    SourceType = sourceType.Value,
                    SourceId = null
                }
                : CreateSettlementRequest(fixture, direction, LedgerSettlementState.Final, amount, businessPartnerId),
            CancellationToken.None);
    }

    private static async Task<(Project Project, Contract Contract)> AddProjectAsync(CentralLedgerTestFixture fixture, string number)
    {
        var project = new Project { ProjectNumber = number, Name = $"项目 {number}", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract
        {
            Project = project,
            ContractNumber = $"C-{number}",
            Name = $"合同 {number}",
            BusinessPartner = fixture.Client,
            TotalAmount = 1_000m
        };
        project.Contracts.Add(contract);
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = fixture.LegalEntity, IsPrimary = true });
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        fixture.GrantProjectAccess(project.Id);
        return (project, contract);
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
