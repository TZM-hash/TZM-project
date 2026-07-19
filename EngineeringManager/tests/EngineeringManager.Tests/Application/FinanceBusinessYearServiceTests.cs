using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class FinanceBusinessYearServiceTests
{
    [Fact]
    public async Task CustomFinanceYearIsIndependentFromEmployeeYearAndResolvesBoundaryDates()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        fixture.Db.BusinessYears.Add(new BusinessYear { Name = "员工年度", StartDate = new DateOnly(2026, 3, 1), EndDate = new DateOnly(2027, 2, 28) });
        await fixture.Db.SaveChangesAsync();
        var actor = fixture.ExternalActor() with { CanManageYears = true };
        var service = new FinanceBusinessYearService(fixture.Db);

        var created = await service.CreateAsync(actor, new CreateFinanceBusinessYearRequest("财务 2026", new DateOnly(2026, 3, 1), new DateOnly(2027, 2, 28)), CancellationToken.None);

        created.Name.Should().Be("财务 2026");
        (await service.ResolveAsync(new DateOnly(2026, 3, 1), CancellationToken.None))!.Id.Should().Be(created.Id);
        (await service.ResolveAsync(new DateOnly(2027, 2, 28), CancellationToken.None))!.Id.Should().Be(created.Id);
        (await service.ResolveAsync(new DateOnly(2027, 3, 1), CancellationToken.None)).Should().BeNull();
        (await fixture.Db.BusinessYears.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task OverlappingFinanceYearsAreRejectedButAdjacentRangesAreAllowed()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var actor = fixture.ExternalActor() with { CanManageYears = true };
        var service = new FinanceBusinessYearService(fixture.Db);
        await service.CreateAsync(actor, new CreateFinanceBusinessYearRequest("第一年度", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        var overlap = () => service.CreateAsync(actor, new CreateFinanceBusinessYearRequest("重叠年度", new DateOnly(2026, 12, 31), new DateOnly(2027, 12, 30)), CancellationToken.None);
        await overlap.Should().ThrowAsync<InvalidOperationException>().WithMessage("*重叠*");
        var adjacent = await service.CreateAsync(actor, new CreateFinanceBusinessYearRequest("第二年度", new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31)), CancellationToken.None);
        adjacent.Name.Should().Be("第二年度");
    }

    [Fact]
    public async Task ListCountsOnlyCentralLedgerRecordsWithinFinanceDateRange()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var actor = fixture.ExternalActor() with { CanManageYears = true };
        var service = new FinanceBusinessYearService(fixture.Db);
        var year = await service.CreateAsync(actor, new CreateFinanceBusinessYearRequest("财务年度", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);
        fixture.Db.FinanceSettlements.Add(new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable,
            SettlementState = LedgerSettlementState.Final, SourceType = LedgerSourceType.CentralLedger,
            Project = fixture.Project, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client,
            BusinessDate = new DateOnly(2026, 6, 1), OriginalAmount = 10m, OriginalInvoiceAmount = 10m
        });
        fixture.Db.FinanceCashEntries.Add(new FinanceCashEntry
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, CashType = LedgerCashType.Collection,
            LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client, BusinessDate = new DateOnly(2026, 6, 2), Amount = 5m
        });
        fixture.Db.FinanceInvoices.Add(new FinanceInvoice
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable,
            LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client, InvoiceNumber = "Y-1", InvoiceDate = new DateOnly(2026, 6, 3), Amount = 5m
        });
        fixture.Db.FinanceSettlements.Add(new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable,
            SettlementState = LedgerSettlementState.Final, SourceType = LedgerSourceType.CentralLedger,
            Project = fixture.Project, LegalEntity = fixture.LegalEntity, BusinessPartner = fixture.Client,
            BusinessDate = new DateOnly(2027, 1, 1), OriginalAmount = 20m, OriginalInvoiceAmount = 20m
        });
        await fixture.Db.SaveChangesAsync();

        var listed = await service.ListAsync(CancellationToken.None);

        listed.Single(item => item.Id == year.Id).RecordCount.Should().Be(3);
    }

    [Fact]
    public async Task DeleteRequiresPermissionReasonAndCurrentConcurrencyStampAndWritesAudit()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var manager = fixture.ExternalActor() with { CanManageYears = true };
        var service = new FinanceBusinessYearService(fixture.Db);
        var year = await service.CreateAsync(manager, new CreateFinanceBusinessYearRequest("待删除", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        var unauthorized = () => service.DeleteAsync(fixture.ExternalActor(), year.Id, year.ConcurrencyStamp, "清理", CancellationToken.None);
        await unauthorized.Should().ThrowAsync<UnauthorizedAccessException>();
        var missingReason = () => service.DeleteAsync(manager, year.Id, year.ConcurrencyStamp, " ", CancellationToken.None);
        await missingReason.Should().ThrowAsync<ArgumentException>();
        var stale = () => service.DeleteAsync(manager, year.Id, Guid.NewGuid(), "清理", CancellationToken.None);
        await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await service.DeleteAsync(manager, year.Id, year.ConcurrencyStamp, "清理错误年度", CancellationToken.None);

        (await fixture.Db.FinanceBusinessYears.CountAsync()).Should().Be(0);
        var audit = await fixture.Db.AuditLogs.SingleAsync(item => item.EntityType == nameof(FinanceBusinessYear) && item.Action == "Delete");
        audit.Reason.Should().Be("清理错误年度");
    }
}
