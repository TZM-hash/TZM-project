using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class FinanceReconciliationServiceTests
{
    [Fact]
    public async Task SnapshotDoesNotLockHistoricalRecordsAndShowsCurrentDifferenceAndNewVersion()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var actor = fixture.ExternalActor() with { CanReconcile = true };
        var commands = new CentralLedgerCommandService(fixture.Db, new CentralLedgerAllocationService(fixture.Db));
        var settlementId = await commands.CreateSettlementAsync(actor, new CreateSettlementRequest(
            LedgerScope.External, LedgerDirection.Receivable, LedgerSettlementState.Final, LedgerSourceType.CentralLedger, null,
            fixture.LegalEntity.Id, fixture.Client.Id, null, fixture.Project.Id, fixture.Contract.Id, null,
            new DateOnly(2026, 7, 1), 100m, 100m, "对账应收"), CancellationToken.None);
        var query = new CentralLedgerQuery(LedgerScope.External, StartDate: new DateOnly(2026, 1, 1), EndDate: new DateOnly(2026, 12, 31));
        var service = new FinanceReconciliationService(fixture.Db, new CentralLedgerQueryService(fixture.Db));

        var firstId = await service.CreateAsync(actor, new CreateFinanceReconciliationRequest(
            LedgerScope.External, FinanceReconciliationScope.WholeLedger, null, null, null,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), query), CancellationToken.None);
        var settlement = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == settlementId);
        await commands.AddDeductionAsync(actor, new AddFinanceDeductionRequest(
            settlementId, new DateOnly(2026, 8, 1), 10m, true, "历史修正", settlement.ConcurrencyStamp), CancellationToken.None);

        var details = await service.GetDetailsAsync(actor, firstId, CancellationToken.None);

        details.Should().NotBeNull();
        details!.Reconciliation.Version.Should().Be(1);
        details.Lines.Should().ContainSingle();
        var line = details.Lines.Single();
        line.SnapshotMetrics.ActualAmount.Should().Be(100m);
        line.CurrentMetrics.ActualAmount.Should().Be(90m);
        line.Difference.ActualAmount.Should().Be(-10m);
        line.WasDeleted.Should().BeFalse();
        var secondId = await service.CreateAsync(actor, new CreateFinanceReconciliationRequest(
            LedgerScope.External, FinanceReconciliationScope.WholeLedger, null, null, null,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), query), CancellationToken.None);
        secondId.Should().NotBe(firstId);
        (await service.ListAsync(actor, query, CancellationToken.None)).Select(item => item.Version).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task PhysicalDeleteAppearsAsDeletedSnapshotDifference()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var actor = fixture.ExternalActor() with { CanReconcile = true };
        var commands = new CentralLedgerCommandService(fixture.Db, new CentralLedgerAllocationService(fixture.Db));
        var settlementId = await commands.CreateSettlementAsync(actor, new CreateSettlementRequest(
            LedgerScope.External, LedgerDirection.Payable, LedgerSettlementState.Final, LedgerSourceType.CentralLedger, null,
            fixture.LegalEntity.Id, fixture.Supplier.Id, null, fixture.Project.Id, fixture.Contract.Id, null,
            new DateOnly(2026, 7, 1), 80m, 80m, null), CancellationToken.None);
        var query = new CentralLedgerQuery(LedgerScope.External);
        var service = new FinanceReconciliationService(fixture.Db, new CentralLedgerQueryService(fixture.Db));
        var snapshotId = await service.CreateAsync(actor, new CreateFinanceReconciliationRequest(
            LedgerScope.External, FinanceReconciliationScope.WholeLedger, null, null, null, null,
            new DateOnly(2026, 12, 31), query), CancellationToken.None);
        var settlement = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == settlementId);

        await commands.DeleteAsync(actor, new DeleteFinanceRecordRequest(
            FinanceRecordType.Settlement, settlementId, settlement.ConcurrencyStamp, "删除错误应付", "test"), CancellationToken.None);

        var details = await service.GetDetailsAsync(actor, snapshotId, CancellationToken.None);
        details!.Lines.Single().WasDeleted.Should().BeTrue();
        details.Lines.Single().CurrentMetrics.Should().Be(CentralLedgerMetrics.Zero);
        details.Lines.Single().Difference.ActualAmount.Should().Be(-80m);
        (await fixture.Db.FinanceDeletionLogs.AnyAsync(item => item.RecordId == settlementId)).Should().BeTrue();
    }

    [Fact]
    public async Task ReconciliationCreateAndDeleteRequireDedicatedPermissionAndReason()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new FinanceReconciliationService(fixture.Db, new CentralLedgerQueryService(fixture.Db));
        var request = new CreateFinanceReconciliationRequest(
            LedgerScope.External, FinanceReconciliationScope.WholeLedger, null, null, null, null,
            new DateOnly(2026, 12, 31), new CentralLedgerQuery(LedgerScope.External));
        var unauthorizedCreate = () => service.CreateAsync(fixture.ExternalActor(), request, CancellationToken.None);
        await unauthorizedCreate.Should().ThrowAsync<UnauthorizedAccessException>();
        var manager = fixture.ExternalActor() with { CanReconcile = true };
        var id = await service.CreateAsync(manager, request, CancellationToken.None);
        var reconciliation = await fixture.Db.FinanceReconciliations.SingleAsync(item => item.Id == id);
        var unauthorizedDelete = () => service.DeleteAsync(fixture.ExternalActor(), id, reconciliation.ConcurrencyStamp, "删除", CancellationToken.None);
        await unauthorizedDelete.Should().ThrowAsync<UnauthorizedAccessException>();
        var noReason = () => service.DeleteAsync(manager, id, reconciliation.ConcurrencyStamp, " ", CancellationToken.None);
        await noReason.Should().ThrowAsync<ArgumentException>();
    }
}
