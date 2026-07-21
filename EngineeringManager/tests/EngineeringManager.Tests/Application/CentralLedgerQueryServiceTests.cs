using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class CentralLedgerQueryServiceTests
{
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
}
