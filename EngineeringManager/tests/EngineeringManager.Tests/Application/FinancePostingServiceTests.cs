using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class FinancePostingServiceTests
{
    [Fact]
    public async Task ConfirmedProjectQuantityUpsertsOneFinalReceivableBySourceId()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        fixture.LineItem.SettledQuantity = 10m;
        fixture.LineItem.SettledUnitPrice = 120m;
        fixture.LineItem.IsSettlementConfirmed = true;
        await fixture.Db.SaveChangesAsync();
        var service = new FinancePostingService(fixture.Db);

        var firstId = await service.UpsertProjectQuantityReceivableAsync(fixture.ExternalActor(), fixture.LineItem.Id, CancellationToken.None);
        var secondId = await service.UpsertProjectQuantityReceivableAsync(fixture.ExternalActor(), fixture.LineItem.Id, CancellationToken.None);

        firstId.Should().Be(secondId);
        var settlement = await fixture.Db.FinanceSettlements.SingleAsync();
        settlement.SourceType.Should().Be(LedgerSourceType.ProjectQuantity);
        settlement.SourceId.Should().Be(fixture.LineItem.Id);
        settlement.SettlementState.Should().Be(LedgerSettlementState.Final);
        settlement.OriginalAmount.Should().Be(1_200m);
    }

    [Fact]
    public async Task EditingQuantityUpdatesTheSameCentralSettlement()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new FinancePostingService(fixture.Db);
        var id = await service.UpsertProjectQuantityReceivableAsync(fixture.ExternalActor(), fixture.LineItem.Id, CancellationToken.None);
        fixture.LineItem.EstimatedQuantity = 2m;
        fixture.LineItem.EstimatedUnitPrice = 750m;
        await fixture.Db.SaveChangesAsync();

        var updatedId = await service.UpsertProjectQuantityReceivableAsync(fixture.ExternalActor(), fixture.LineItem.Id, CancellationToken.None);

        updatedId.Should().Be(id);
        var settlement = await fixture.Db.FinanceSettlements.SingleAsync();
        settlement.OriginalAmount.Should().Be(1_500m);
        settlement.ConcurrencyStamp.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task UnconfirmedQuantityUsesEstimatedAmountAsProvisionalSettlement()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        fixture.LineItem.IsSettlementConfirmed = false;
        fixture.LineItem.EstimatedQuantity = 3m;
        fixture.LineItem.EstimatedUnitPrice = 400m;
        fixture.LineItem.SettledQuantity = null;
        fixture.LineItem.SettledUnitPrice = null;
        await fixture.Db.SaveChangesAsync();
        var service = new FinancePostingService(fixture.Db);

        await service.UpsertProjectQuantityReceivableAsync(fixture.ExternalActor(), fixture.LineItem.Id, CancellationToken.None);

        var settlement = await fixture.Db.FinanceSettlements.SingleAsync();
        settlement.SettlementState.Should().Be(LedgerSettlementState.Provisional);
        settlement.OriginalAmount.Should().Be(1_200m);
        settlement.OriginalInvoiceAmount.Should().Be(1_200m);
    }

    [Fact]
    public async Task CrewCanCreateStandalonePayable()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new FinancePostingService(fixture.Db);

        var id = await service.CreateCrewPayableAsync(
            fixture.ExternalActor(),
            new CreateCrewPayableRequest(
                fixture.Crew.Id,
                fixture.LegalEntity.Id,
                fixture.Project.Id,
                fixture.Contract.Id,
                new DateOnly(2026, 7, 19),
                LedgerSettlementState.Final,
                500m,
                500m,
                "班组独立应付"),
            CancellationToken.None);

        var settlement = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == id);
        settlement.Direction.Should().Be(LedgerDirection.Payable);
        settlement.SourceType.Should().Be(LedgerSourceType.Crew);
        settlement.BusinessPartnerId.Should().Be(fixture.Crew.Id);
    }

    [Fact]
    public async Task PartnerCanCreateStandalonePayable()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var service = new FinancePostingService(fixture.Db);

        var id = await service.CreatePartnerPayableAsync(
            fixture.ExternalActor(),
            new CreatePartnerPayableRequest(
                fixture.Supplier.Id,
                fixture.LegalEntity.Id,
                fixture.Project.Id,
                fixture.Contract.Id,
                new DateOnly(2026, 7, 19),
                LedgerSettlementState.Provisional,
                300m,
                280m,
                "合作商暂估应付"),
            CancellationToken.None);

        var settlement = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Id == id);
        settlement.SourceType.Should().Be(LedgerSourceType.Partner);
        settlement.SettlementState.Should().Be(LedgerSettlementState.Provisional);
        settlement.OriginalInvoiceAmount.Should().Be(280m);
    }
}
