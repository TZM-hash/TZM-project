using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class CentralLedgerAllocationServiceTests
{
    [Fact]
    public async Task ManualInvoiceCanAllocateAcrossProjectsAndSettlements()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = CreateCommand(fixture);
        var firstId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 1), 800m);
        var (secondProject, secondContract) = await AddProjectAsync(fixture, "P-2");
        var secondId = await CreateManualSettlementAsync(command, fixture, secondProject, secondContract, new DateOnly(2026, 7, 2), 700m);

        var invoiceId = await command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            InvoiceRequest(fixture, 1_000m,
            [
                new FinanceAllocationRequest(firstId, 600m, 1),
                new FinanceAllocationRequest(secondId, 400m, 2)
            ]),
            CancellationToken.None);

        var allocations = await fixture.Db.FinanceInvoiceAllocations.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId)
            .OrderBy(item => item.AllocationOrder)
            .ToListAsync();
        allocations.Select(item => item.ProjectId).Should().Equal(fixture.Project.Id, secondProject.Id);
        allocations.Select(item => item.Amount).Should().Equal(600m, 400m);
    }

    [Fact]
    public async Task ManualCashCanAllocateAcrossProjectsAndSettlements()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = CreateCommand(fixture);
        var firstId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 1), 800m);
        var (secondProject, secondContract) = await AddProjectAsync(fixture, "P-3");
        var secondId = await CreateManualSettlementAsync(command, fixture, secondProject, secondContract, new DateOnly(2026, 7, 2), 700m);

        var cashId = await command.CreateCashAsync(
            fixture.ExternalActor(),
            CashRequest(fixture, 900m,
            [
                new FinanceAllocationRequest(firstId, 500m, 1),
                new FinanceAllocationRequest(secondId, 400m, 2)
            ]),
            CancellationToken.None);

        var allocations = await fixture.Db.FinanceCashAllocations.AsNoTracking()
            .Where(item => item.CashEntryId == cashId)
            .OrderBy(item => item.AllocationOrder)
            .ToListAsync();
        allocations.Select(item => item.ProjectId).Should().Equal(fixture.Project.Id, secondProject.Id);
        allocations.Select(item => item.Amount).Should().Equal(500m, 400m);
    }

    [Fact]
    public async Task AutomaticInvoiceUsesOldestUninvoicedSettlementFirst()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = CreateCommand(fixture);
        var oldestId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 6, 1), 1_000m);
        var newestId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 1), 1_000m);

        var invoiceId = await command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            InvoiceRequest(fixture, 1_200m, [], autoAllocate: true),
            CancellationToken.None);

        var allocations = await fixture.Db.FinanceInvoiceAllocations.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId)
            .OrderBy(item => item.AllocationOrder)
            .ToListAsync();
        allocations.Select(item => item.SettlementId).Should().Equal(oldestId, newestId);
        allocations.Select(item => item.Amount).Should().Equal(1_000m, 200m);
    }

    [Fact]
    public async Task AutomaticCashUsesOldestUnsettledBalanceFirst()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = CreateCommand(fixture);
        var oldestId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 6, 1), 1_000m);
        var newestId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 1), 1_000m);

        var cashId = await command.CreateCashAsync(
            fixture.ExternalActor(),
            CashRequest(fixture, 1_300m, [], autoAllocate: true),
            CancellationToken.None);

        var allocations = await fixture.Db.FinanceCashAllocations.AsNoTracking()
            .Where(item => item.CashEntryId == cashId)
            .OrderBy(item => item.AllocationOrder)
            .ToListAsync();
        allocations.Select(item => item.SettlementId).Should().Equal(oldestId, newestId);
        allocations.Select(item => item.Amount).Should().Equal(1_000m, 300m);
    }

    [Fact]
    public async Task AutomaticMatchingNeverCrossesLegalEntityOrCounterparty()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = CreateCommand(fixture);
        var clientId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 1), 500m);
        await command.CreateSettlementAsync(
            fixture.ExternalActor(),
            new CreateSettlementRequest(
                LedgerScope.External,
                LedgerDirection.Receivable,
                LedgerSettlementState.Final,
                LedgerSourceType.CentralLedger,
                null,
                fixture.LegalEntity.Id,
                fixture.Supplier.Id,
                null,
                fixture.Project.Id,
                fixture.Contract.Id,
                null,
                new DateOnly(2026, 6, 1),
                1_000m,
                1_000m,
                null),
            CancellationToken.None);

        var invoiceId = await command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            InvoiceRequest(fixture, 800m, [], autoAllocate: true),
            CancellationToken.None);

        var allocations = await fixture.Db.FinanceInvoiceAllocations.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId)
            .ToListAsync();
        allocations.Should().ContainSingle();
        allocations.Single().SettlementId.Should().Be(clientId);
        allocations.Single().Amount.Should().Be(500m);
    }

    [Fact]
    public async Task AutomaticMatchingLeavesRemainderUnallocated()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = CreateCommand(fixture);
        await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 1), 500m);

        var invoiceId = await command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            InvoiceRequest(fixture, 800m, [], autoAllocate: true),
            CancellationToken.None);

        var invoice = await fixture.Db.FinanceInvoices.AsNoTracking().Include(item => item.Allocations).SingleAsync(item => item.Id == invoiceId);
        invoice.Allocations.Sum(item => item.Amount).Should().Be(500m);
        (invoice.Amount - invoice.Allocations.Sum(item => item.Amount)).Should().Be(300m);
    }

    [Fact]
    public async Task ReplacingAllocationsRecalculatesEveryAffectedSummary()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = CreateCommand(fixture);
        var firstId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 1), 1_000m);
        var secondId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 2), 1_000m);
        var invoiceId = await command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            InvoiceRequest(fixture, 600m, [new FinanceAllocationRequest(firstId, 600m, 1)]),
            CancellationToken.None);
        var stamp = (await fixture.Db.FinanceInvoices.SingleAsync(item => item.Id == invoiceId)).ConcurrencyStamp;

        await command.ReplaceInvoiceAllocationsAsync(
            fixture.ExternalActor(),
            new ReplaceInvoiceAllocationsRequest(invoiceId, [new FinanceAllocationRequest(secondId, 600m, 1)], stamp, "调整分摊项目"),
            CancellationToken.None);

        (await InvoicedAmountAsync(fixture.Db, firstId)).Should().Be(0m);
        (await InvoicedAmountAsync(fixture.Db, secondId)).Should().Be(600m);
    }

    [Fact]
    public async Task AllocationCannotExceedHeaderEffectiveAmount()
    {
        await using var fixture = await CentralLedgerTestFixture.CreateAsync();
        var command = CreateCommand(fixture);
        var settlementId = await CreateManualSettlementAsync(command, fixture, fixture.Project, fixture.Contract, new DateOnly(2026, 7, 1), 1_000m);

        var action = () => command.CreateInvoiceAsync(
            fixture.ExternalActor(),
            InvoiceRequest(fixture, 500m, [new FinanceAllocationRequest(settlementId, 600m, 1)]),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*分摊金额合计*");
    }

    private static CentralLedgerCommandService CreateCommand(CentralLedgerTestFixture fixture)
    {
        var allocation = new CentralLedgerAllocationService(fixture.Db);
        return new CentralLedgerCommandService(fixture.Db, allocation);
    }

    private static async Task<Guid> CreateManualSettlementAsync(
        CentralLedgerCommandService command,
        CentralLedgerTestFixture fixture,
        Project project,
        Contract contract,
        DateOnly businessDate,
        decimal amount)
    {
        return await command.CreateSettlementAsync(
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
                project.Id,
                contract.Id,
                null,
                businessDate,
                amount,
                amount,
                null),
            CancellationToken.None);
    }

    private static CreateFinanceInvoiceRequest InvoiceRequest(
        CentralLedgerTestFixture fixture,
        decimal amount,
        IReadOnlyList<FinanceAllocationRequest> allocations,
        bool autoAllocate = false)
    {
        return new CreateFinanceInvoiceRequest(
            LedgerScope.External,
            LedgerDirection.Receivable,
            LedgerSourceType.CentralLedger,
            null,
            fixture.LegalEntity.Id,
            fixture.Client.Id,
            null,
            $"AUTO-{Guid.NewGuid():N}",
            new DateOnly(2026, 7, 20),
            amount,
            null,
            null,
            null,
            null,
            allocations,
            autoAllocate);
    }

    private static CreateFinanceCashRequest CashRequest(
        CentralLedgerTestFixture fixture,
        decimal amount,
        IReadOnlyList<FinanceAllocationRequest> allocations,
        bool autoAllocate = false)
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
            new DateOnly(2026, 7, 20),
            amount,
            "银行转账",
            null,
            allocations,
            autoAllocate);
    }

    private static async Task<(Project Project, Contract Contract)> AddProjectAsync(CentralLedgerTestFixture fixture, string number)
    {
        var project = new Project { ProjectNumber = number, Name = $"项目 {number}", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract { Project = project, ContractNumber = $"C-{number}", Name = $"合同 {number}", BusinessPartner = fixture.Client, TotalAmount = 1_000m };
        project.Contracts.Add(contract);
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        fixture.GrantProjectAccess(project.Id);
        return (project, contract);
    }

    private static async Task<decimal> InvoicedAmountAsync(ApplicationDbContext db, Guid settlementId)
    {
        return await db.FinanceInvoiceAllocations.AsNoTracking()
            .Where(item => item.SettlementId == settlementId && item.Invoice.Status == LedgerRecordStatus.Active)
            .SumAsync(item => (decimal?)item.Amount) ?? 0m;
    }
}
