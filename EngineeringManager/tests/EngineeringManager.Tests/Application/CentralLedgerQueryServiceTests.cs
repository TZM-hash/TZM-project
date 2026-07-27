using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class CentralLedgerQueryServiceTests
{
    [Fact]
    public async Task ExternalLedgerReadsEffectivePayrollPaymentsWithoutCreatingFinanceCopies()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        AddPayrollBatch(fixture, "PAY-DRAFT", PayrollBatchStatus.Draft, 100m);
        var confirmed = AddPayrollBatch(fixture, "PAY-CONFIRMED", PayrollBatchStatus.Confirmed, 1_000m);
        var closed = AddPayrollBatch(fixture, "PAY-CLOSED", PayrollBatchStatus.Closed, 2_000m);
        AddPayrollBatch(fixture, "PAY-VOIDED", PayrollBatchStatus.Voided, 4_000m);
        await fixture.Db.SaveChangesAsync();
        var transactionCount = await fixture.Db.AccountTransactions.CountAsync();
        var cashCount = await fixture.Db.FinanceCashEntries.CountAsync();

        var result = await new CentralLedgerQueryService(fixture.Db).SearchAsync(
            fixture.ExternalActor(),
            new CentralLedgerQuery(LedgerScope.External),
            CancellationToken.None);

        var paymentsProperty = result.GetType().GetProperty("PayrollPayments");
        paymentsProperty.Should().NotBeNull("the central ledger result needs a first-class payroll payment projection");
        var payments = ((System.Collections.IEnumerable)paymentsProperty!.GetValue(result)!).Cast<object>().ToArray();
        payments.Should().HaveCount(2);
        payments.Select(item => (Guid)item.GetType().GetProperty("BatchId")!.GetValue(item)!)
            .Should().BeEquivalentTo([confirmed.Id, closed.Id]);
        var totalProperty = result.GetType().GetProperty("PayrollPaymentTotal");
        totalProperty.Should().NotBeNull();
        ((decimal)totalProperty!.GetValue(result)!).Should().Be(3_000m);
        (await fixture.Db.AccountTransactions.CountAsync()).Should().Be(transactionCount);
        (await fixture.Db.FinanceCashEntries.CountAsync()).Should().Be(cashCount);
    }

    [Fact]
    public async Task SearchFiltersByScopeCompanyProjectPartnerDateAndSettlementState()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var query = new CentralLedgerQueryService(fixture.Db);
        var targetId = await command.CreateSettlementAsync(
            fixture.ExternalActor(),
            SettlementRequest(fixture, LedgerSettlementState.Provisional, new DateOnly(2026, 7, 10), 800m, "筛选目标"),
            CancellationToken.None);
        await command.CreateSettlementAsync(
            fixture.ExternalActor(),
            SettlementRequest(fixture, LedgerSettlementState.Final, new DateOnly(2026, 6, 10), 900m, "范围外记录"),
            CancellationToken.None);

        var result = await query.SearchAsync(
            fixture.ExternalActor(),
            new CentralLedgerQuery(
                LedgerScope.External,
                LedgerDirection.Receivable,
                StartDate: new DateOnly(2026, 7, 1),
                EndDate: new DateOnly(2026, 7, 31),
                LegalEntityId: fixture.LegalEntity.Id,
                BusinessPartnerId: fixture.Client.Id,
                ProjectId: fixture.Project.Id,
                ContractId: fixture.Contract.Id,
                SettlementState: LedgerSettlementState.Provisional),
            CancellationToken.None);

        result.Rows.Should().ContainSingle();
        result.Rows.Single().SettlementId.Should().Be(targetId);
        result.Totals.GrossSettlementAmount.Should().Be(800m);
    }

    [Fact]
    public async Task SearchUsesWhitespaceFullFieldTermsAndTotalsAllMatchingRowsBeforePaging()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var query = new CentralLedgerQueryService(fixture.Db);
        for (var index = 1; index <= 3; index++)
        {
            await command.CreateSettlementAsync(
                fixture.ExternalActor(),
                SettlementRequest(fixture, LedgerSettlementState.Final, new DateOnly(2026, 7, index), index * 100m, $"中央 客户 第{index}笔"),
                CancellationToken.None);
        }

        var result = await query.SearchAsync(
            fixture.ExternalActor(),
            new CentralLedgerQuery(LedgerScope.External, Search: "中央 客户", Page: 1, PageSize: 1),
            CancellationToken.None);

        result.Rows.Should().ContainSingle();
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(3);
        result.MatchingSettlementIds.Should().HaveCount(3);
        result.Totals.GrossSettlementAmount.Should().Be(600m);
    }

    [Fact]
    public async Task AdvanceInvoiceCashAndOverSettlementCashRemainIndependentFilters()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var query = new CentralLedgerQueryService(fixture.Db);
        var settlementId = await command.CreateSettlementAsync(
            fixture.ExternalActor(),
            SettlementRequest(fixture, LedgerSettlementState.Final, new DateOnly(2026, 7, 1), 1_000m, null),
            CancellationToken.None);
        await command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "QUERY-OUT-001",
                new DateOnly(2026, 7, 2),
                400m,
                null,
                null,
                null,
                null,
                [new FinanceAllocationRequest(settlementId, 400m, 1)]),
            CancellationToken.None);
        await command.CreateCashAsync(
            fixture.ExternalActor(),
            CashRequest(fixture, settlementId, 600m, 1),
            CancellationToken.None);

        var advanceOnly = await query.SearchAsync(
            fixture.ExternalActor(),
            new CentralLedgerQuery(LedgerScope.External, HasAdvanceInvoiceCash: true, HasOverSettlementCash: false),
            CancellationToken.None);
        advanceOnly.Rows.Should().ContainSingle();
        advanceOnly.Rows.Single().Metrics.AdvanceInvoiceCash.Should().Be(200m);
        advanceOnly.Rows.Single().Metrics.OverSettlementCash.Should().Be(0m);

        await command.CreateCashAsync(
            fixture.ExternalActor(),
            CashRequest(fixture, settlementId, 500m, 2),
            CancellationToken.None);
        var overSettlement = await query.SearchAsync(
            fixture.ExternalActor(),
            new CentralLedgerQuery(LedgerScope.External, HasOverSettlementCash: true),
            CancellationToken.None);
        overSettlement.Rows.Should().ContainSingle();
        overSettlement.Rows.Single().Metrics.CashAmount.Should().Be(1_100m);
        overSettlement.Rows.Single().Metrics.OverSettlementCash.Should().Be(100m);
    }

    [Fact]
    public async Task ProjectAndPartnerMetricsUseTheSameAuthorizedCentralRecords()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var query = new CentralLedgerQueryService(fixture.Db);
        await command.CreateSettlementAsync(
            fixture.ExternalActor(),
            SettlementRequest(fixture, LedgerSettlementState.Final, new DateOnly(2026, 7, 1), 700m, null),
            CancellationToken.None);

        var project = await query.GetProjectMetricsAsync(fixture.ExternalActor(), fixture.Project.Id, CancellationToken.None);
        var partner = await query.GetPartnerMetricsAsync(fixture.ExternalActor(), fixture.Client.Id, CancellationToken.None);

        project.Should().Be(partner);
        project.ActualAmount.Should().Be(700m);
        project.UncollectedOrUnpaid.Should().Be(700m);
    }

    [Fact]
    public async Task PartnerSummariesSeparateReceivablesPayablesAndReturnEmptyRequestedPartners()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var query = new CentralLedgerQueryService(fixture.Db);
        var actor = fixture.ExternalActor();
        var emptyPartnerId = Guid.NewGuid();

        var receivableId = await command.CreateSettlementAsync(
            actor,
            new CreateSettlementRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSettlementState.Final,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                fixture.Project.Id,
                fixture.Contract.Id,
                null,
                new DateOnly(2026, 7, 1),
                1_000m,
                1_000m,
                "客户应收"),
            CancellationToken.None);
        var payableId = await command.CreateSettlementAsync(
            actor,
            new CreateSettlementRequest(
                LedgerScope.External,
                LedgerDirection.Payable,
                LedgerSettlementState.Final,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Supplier.Id,
                null,
                fixture.Project.Id,
                null,
                null,
                new DateOnly(2026, 7, 2),
                750m,
                750m,
                "供应商应付"),
            CancellationToken.None);

        await command.CreateInvoiceAsync(
            actor,
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                "PARTNER-OUT-001",
                new DateOnly(2026, 7, 3),
                800m,
                null,
                null,
                null,
                "客户销项",
                [new FinanceAllocationRequest(receivableId, 800m, 1)]),
            CancellationToken.None);
        await command.CreateInvoiceAsync(
            actor,
            new CreateFinanceInvoiceRequest(
                LedgerScope.External,
                LedgerDirection.Payable,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Supplier.Id,
                null,
                "PARTNER-IN-001",
                new DateOnly(2026, 7, 4),
                600m,
                null,
                null,
                null,
                "供应商进项",
                [new FinanceAllocationRequest(payableId, 600m, 1)]),
            CancellationToken.None);
        await command.CreateCashAsync(
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
                new DateOnly(2026, 7, 5),
                650m,
                "银行转账",
                "客户收款",
                [new FinanceAllocationRequest(receivableId, 650m, 1)]),
            CancellationToken.None);
        await command.CreateCashAsync(
            actor,
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
                new DateOnly(2026, 7, 6),
                500m,
                "银行转账",
                "供应商付款",
                [new FinanceAllocationRequest(payableId, 500m, 1)]),
            CancellationToken.None);

        var summaries = await query.GetPartnerSummariesAsync(
            actor,
            [fixture.Client.Id, fixture.Supplier.Id, emptyPartnerId, fixture.Client.Id],
            CancellationToken.None);

        summaries.Should().HaveCount(3);
        summaries[fixture.Client.Id].Receivable.CashAmount.Should().Be(650m);
        summaries[fixture.Client.Id].Receivable.UncollectedOrUnpaid.Should().Be(350m);
        summaries[fixture.Client.Id].Receivable.InvoicedAmount.Should().Be(800m);
        summaries[fixture.Client.Id].Payable.Should().Be(CentralLedgerMetrics.Zero);
        summaries[fixture.Supplier.Id].Payable.CashAmount.Should().Be(500m);
        summaries[fixture.Supplier.Id].Payable.UncollectedOrUnpaid.Should().Be(250m);
        summaries[fixture.Supplier.Id].Payable.InvoicedAmount.Should().Be(600m);
        summaries[fixture.Supplier.Id].Receivable.Should().Be(CentralLedgerMetrics.Zero);
        summaries[emptyPartnerId].Should().Be(PartnerLedgerSummaryDto.Empty(emptyPartnerId));
    }

    [Fact]
    public async Task ReadOnlyActorCanQueryAuthorizedRowsButNotUnauthorizedCompanyRows()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var query = new CentralLedgerQueryService(fixture.Db);
        await command.CreateSettlementAsync(
            fixture.ExternalActor(),
            SettlementRequest(fixture, LedgerSettlementState.Final, new DateOnly(2026, 7, 1), 500m, null),
            CancellationToken.None);

        var result = await query.SearchAsync(
            fixture.ReadOnlyActor(),
            new CentralLedgerQuery(LedgerScope.External),
            CancellationToken.None);
        Func<Task> unauthorized = async () => await query.SearchAsync(
            fixture.ReadOnlyActor(),
            new CentralLedgerQuery(LedgerScope.External, LegalEntityId: fixture.CounterLegalEntity.Id),
            CancellationToken.None);

        result.Rows.Should().ContainSingle();
        await unauthorized.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SearchListsProjectOwnedCashThatHasNotBeenAllocated()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = new CentralLedgerCommandService(fixture.Db);
        var cashId = await command.CreateCashAsync(
            fixture.ExternalActor(),
            new CreateFinanceCashRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerCashType.Collection,
                LedgerSourceType.ProjectCollection,
                fixture.Project.Id,
                fixture.LegalEntity.Id,
                fixture.Client.Id,
                null,
                fixture.CollectionAccount.Id,
                null,
                new DateOnly(2026, 7, 5),
                250m,
                "项目收款",
                "超额待分摊",
                [],
                ProjectId: fixture.Project.Id),
            CancellationToken.None);

        var result = await new CentralLedgerQueryService(fixture.Db).SearchAsync(
            fixture.ExternalActor(),
            new CentralLedgerQuery(LedgerScope.External, ProjectId: fixture.Project.Id),
            CancellationToken.None);

        result.UnallocatedCash.Should().ContainSingle();
        result.UnallocatedCash.Single().CashEntryId.Should().Be(cashId);
        result.UnallocatedCash.Single().UnallocatedAmount.Should().Be(250m);
    }

    private static CreateSettlementRequest SettlementRequest(
        CentralLedgerTestFixture fixture,
        LedgerSettlementState state,
        DateOnly businessDate,
        decimal amount,
        string? notes)
    {
        return new CreateSettlementRequest(
            LedgerScope.External,
            LedgerDirection.Receivable,
            state,
            LedgerSourceType.CentralLedger,
            null,
            fixture.LegalEntity.Id,
            fixture.Client.Id,
            null,
            fixture.Project.Id,
            fixture.Contract.Id,
            null,
            businessDate,
            amount,
            amount,
            notes);
    }

    private static CreateFinanceCashRequest CashRequest(
        CentralLedgerTestFixture fixture,
        Guid settlementId,
        decimal amount,
        int order)
    {
        return new CreateFinanceCashRequest(
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
            new DateOnly(2026, 7, 3),
            amount,
            "银行转账",
            null,
            [new FinanceAllocationRequest(settlementId, amount, order)]);
    }

    private static PayrollBatch AddPayrollBatch(
        CentralLedgerTestFixture fixture,
        string number,
        PayrollBatchStatus status,
        decimal amount)
    {
        var employee = new Employee { EmployeeNumber = number + "-E", Name = number + " 员工" };
        var worker = new ConstructionWorker { Name = number + " 班组人员" };
        var batch = new PayrollBatch
        {
            BatchNumber = number,
            Name = number + " 工资",
            BatchType = PayrollBatchType.Temporary,
            StartDate = new DateOnly(2026, 7, 18),
            EndDate = new DateOnly(2026, 7, 18),
            PaymentDate = new DateOnly(2026, 7, 18),
            Project = fixture.Project,
            LegalEntity = fixture.LegalEntity,
            Account = fixture.PaymentAccount,
            ActualAmount = amount,
            IsUnifiedDisbursement = true,
            Status = status
        };
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.Employee,
            RecipientKey = $"employee:{batch.Id:N}",
            Employee = employee,
            Amount = amount * 0.6m,
            PayeeName = "工资员工",
            RecipientNameSnapshot = "工资员工"
        });
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.CrewWorker,
            RecipientKey = $"crew-worker:{batch.Id:N}",
            ConstructionWorker = worker,
            CrewBusinessPartner = fixture.Crew,
            Amount = amount * 0.4m,
            PayeeName = "班组人员",
            RecipientNameSnapshot = "班组人员",
            CrewNameSnapshot = fixture.Crew.Name
        });
        var transaction = new AccountTransaction
        {
            Account = fixture.PaymentAccount,
            Direction = AccountTransactionDirection.Outflow,
            SourceType = AccountTransactionSourceType.PayrollPayment,
            SourceId = batch.Id,
            TransactionDate = batch.PaymentDate.Value,
            Amount = amount,
            Description = number + " 工资付款"
        };
        batch.AccountTransactionId = transaction.Id;
        fixture.Db.AddRange(employee, worker, batch, transaction);
        return batch;
    }
}
