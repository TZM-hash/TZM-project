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

public sealed class FinanceSummaryTests
{
    [Fact]
    public async Task AccountsAndProjectOverviewUseServiceCalculatedBalances()
    {
        await using var fixture = await FinanceSummaryFixture.CreateAsync();
        var accountId = await fixture.Service.CreateAccountAsync(
            new CreateFinancialAccountRequest(fixture.LegalEntity.Id, "经营账户", "62220001", "测试银行", FinancialAccountType.Bank, 100m),
            CancellationToken.None);
        var receivableId = await fixture.Service.AddReceivableAsync(
            new CreateReceivableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 16), null, 50m, null),
            CancellationToken.None);
        await fixture.Service.RecordCollectionAsync(
            new RecordCollectionRequest(receivableId, fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, accountId, new DateOnly(2026, 7, 17), 20m, PaymentMethod.BankTransfer, null),
            CancellationToken.None);

        var accounts = await fixture.Service.ListAccountsAsync(CancellationToken.None);
        var projects = await fixture.Service.ListProjectSummariesAsync(CancellationToken.None);
        var overview = await fixture.Service.GetOverviewAsync(CancellationToken.None);
        var options = await fixture.Service.GetEntryOptionsAsync(CancellationToken.None);

        accounts.Should().ContainSingle(item => item.Id == accountId && item.CurrentBalance == 120m);
        projects.Should().ContainSingle(item =>
            item.ProjectId == fixture.Project.Id &&
            item.ProjectNumber == fixture.Project.ProjectNumber &&
            item.Summary.ReceivableAmount == 50m &&
            item.Summary.CollectedAmount == 20m);
        overview.Total.ReceivableAmount.Should().Be(50m);
        overview.Total.CollectedAmount.Should().Be(20m);
        overview.Projects.Should().HaveCount(1);
        options.Projects.Should().ContainSingle(item => item.Id == fixture.Project.Id);
        options.Contracts.Should().ContainSingle(item => item.Id == fixture.Contract.Id);
        options.LegalEntities.Should().ContainSingle(item => item.Id == fixture.LegalEntity.Id && item.Label == fixture.LegalEntity.Name);
        options.BusinessPartners.Should().ContainSingle(item => item.Id == fixture.Partner.Id);
        options.Accounts.Should().ContainSingle(item => item.Id == accountId && item.ParentId == fixture.LegalEntity.Id);
    }

    [Fact]
    public async Task OutputInvoiceCanLinkMultipleReceivablesAndLineItemsAndUpdatesSummary()
    {
        await using var fixture = await FinanceSummaryFixture.CreateAsync();
        var firstReceivable = QuantityReceivable(fixture, fixture.FirstLine, new DateOnly(2026, 7, 16), 60m, "第一笔应收");
        var secondReceivable = QuantityReceivable(fixture, fixture.SecondLine, new DateOnly(2026, 7, 17), 40m, "第二笔应收");
        fixture.Db.FinanceSettlements.AddRange(firstReceivable, secondReceivable);
        await fixture.Db.SaveChangesAsync();

        var invoiceId = await fixture.Service.AddInvoiceAsync(
            new CreateInvoiceRequest(
                fixture.Project.Id,
                fixture.Contract.Id,
                fixture.LegalEntity.Id,
                fixture.Partner.Id,
                InvoiceDirection.Output,
                "OUT-001",
                new DateOnly(2026, 7, 18),
                fixture.SpecialTaxConfiguration.Id,
                61.95m,
                8.05m,
                70m,
                InvoiceStatus.IssuedOrReceived,
                [new InvoiceAllocationRequest(firstReceivable.Id, 50m), new InvoiceAllocationRequest(secondReceivable.Id, 20m)],
                [new InvoiceAllocationRequest(fixture.FirstLine.Id, 40m), new InvoiceAllocationRequest(fixture.SecondLine.Id, 30m)]),
            CancellationToken.None);

        (await fixture.Db.FinanceInvoiceAllocations.CountAsync(item => item.InvoiceId == invoiceId)).Should().Be(2);
        var summary = await fixture.Service.GetSummaryAsync(
            new FinanceSummaryFilter(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id),
            CancellationToken.None);
        summary.ReceivableAmount.Should().Be(100m);
        summary.OutputInvoiceAmount.Should().Be(70m);
        summary.UninvoicedAmount.Should().Be(30m);
    }

    private static FinanceSettlement QuantityReceivable(FinanceSummaryFixture fixture, ContractLineItem line, DateOnly date, decimal amount, string notes) => new()
    {
        Scope = LedgerScope.External,
        Direction = LedgerDirection.Receivable,
        SettlementState = LedgerSettlementState.Provisional,
        SourceType = LedgerSourceType.ProjectQuantity,
        SourceId = line.Id,
        ProjectId = fixture.Project.Id,
        ContractId = fixture.Contract.Id,
        ContractLineItemId = line.Id,
        LegalEntityId = fixture.LegalEntity.Id,
        BusinessPartnerId = fixture.Partner.Id,
        BusinessDate = date,
        OriginalAmount = amount,
        OriginalInvoiceAmount = amount,
        Notes = notes
    };

    [Fact]
    public async Task InputInvoiceIsReportedSeparatelyAndInvalidAllocationsAreRejected()
    {
        await using var fixture = await FinanceSummaryFixture.CreateAsync();
        await fixture.Service.AddPayableAsync(
            new CreatePayableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, PayableSourceType.Manual, new DateOnly(2026, 7, 16), null, 80m, null),
            CancellationToken.None);

        await fixture.Service.AddInvoiceAsync(
            new CreateInvoiceRequest(
                fixture.Project.Id,
                fixture.Contract.Id,
                fixture.LegalEntity.Id,
                fixture.Partner.Id,
                InvoiceDirection.Input,
                "IN-001",
                new DateOnly(2026, 7, 18),
                fixture.OrdinaryTaxConfiguration.Id,
                48.54m,
                1.46m,
                50m,
                InvoiceStatus.IssuedOrReceived,
                [],
                [new InvoiceAllocationRequest(fixture.FirstLine.Id, 50m)]),
            CancellationToken.None);

        var invalidAction = () => fixture.Service.AddInvoiceAsync(
            new CreateInvoiceRequest(
                fixture.Project.Id,
                fixture.Contract.Id,
                fixture.LegalEntity.Id,
                fixture.Partner.Id,
                InvoiceDirection.Output,
                "OUT-BAD",
                new DateOnly(2026, 7, 19),
                fixture.OrdinaryTaxConfiguration.Id,
                60m,
                0m,
                60m,
                InvoiceStatus.IssuedOrReceived,
                [],
                [new InvoiceAllocationRequest(fixture.FirstLine.Id, 40m)]),
            CancellationToken.None);

        await invalidAction.Should().ThrowAsync<ArgumentException>().WithMessage("*分配金额*");
        var summary = await fixture.Service.GetProjectSummaryAsync(fixture.Project.Id, CancellationToken.None);
        summary.PayableAmount.Should().Be(80m);
        summary.InputInvoiceAmount.Should().Be(50m);
        (await fixture.Db.FinanceInvoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ContractSummaryExcludesDeductionsFromOtherContracts()
    {
        await using var fixture = await FinanceSummaryFixture.CreateAsync();
        var otherContract = new Contract
        {
            ProjectId = fixture.Project.Id,
            BusinessPartnerId = fixture.Partner.Id,
            ContractNumber = "FIN-SUM-C2",
            Name = "其他合同",
            TotalAmount = 40m
        };
        fixture.Db.Contracts.Add(otherContract);
        await fixture.Db.SaveChangesAsync();
        var firstPayableId = await fixture.Service.AddPayableAsync(
            new CreatePayableRequest(fixture.Project.Id, fixture.Contract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, PayableSourceType.Manual, new DateOnly(2026, 7, 16), null, 80m, null),
            CancellationToken.None);
        var secondPayableId = await fixture.Service.AddPayableAsync(
            new CreatePayableRequest(fixture.Project.Id, otherContract.Id, fixture.LegalEntity.Id, fixture.Partner.Id, PayableSourceType.Manual, new DateOnly(2026, 7, 16), null, 40m, null),
            CancellationToken.None);
        await fixture.Service.AddDeductionAsync(new CreateDeductionRequest(firstPayableId, fixture.Project.Id, fixture.LegalEntity.Id, fixture.Partner.Id, new DateOnly(2026, 7, 17), 5m, "本合同扣款"), CancellationToken.None);
        await fixture.Service.AddDeductionAsync(new CreateDeductionRequest(secondPayableId, fixture.Project.Id, fixture.LegalEntity.Id, fixture.Partner.Id, new DateOnly(2026, 7, 17), 7m, "其他合同扣款"), CancellationToken.None);

        var summary = await fixture.Service.GetSummaryAsync(new FinanceSummaryFilter(fixture.Project.Id, fixture.Contract.Id), CancellationToken.None);

        summary.PayableAmount.Should().Be(80m);
        summary.DeductionAmount.Should().Be(5m);
        summary.UnpaidAmount.Should().Be(75m);
    }

    private sealed class FinanceSummaryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private FinanceSummaryFixture(SqliteConnection connection, ApplicationDbContext db, IFinanceLedgerService service)
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
        public ContractLineItem FirstLine { get; private set; } = null!;
        public ContractLineItem SecondLine { get; private set; } = null!;
        public ProjectTaxConfiguration OrdinaryTaxConfiguration { get; private set; } = null!;
        public ProjectTaxConfiguration SpecialTaxConfiguration { get; private set; } = null!;

        public static async Task<FinanceSummaryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new FinanceSummaryFixture(connection, db, new FinanceLedgerService(db));
            await fixture.SeedAsync();
            return fixture;
        }

        private async Task SeedAsync()
        {
            LegalEntity = new LegalEntity { Code = "FIN-SUM-LE", Name = "财务汇总公司", ShortName = "汇总公司" };
            Partner = new BusinessPartner { PartnerNumber = "FIN-SUM-BP", Name = "财务汇总合作单位", ShortName = "汇总单位" };
            Project = new Project { ProjectNumber = "FIN-SUM-P", Name = "财务汇总项目", Stage = ProjectStage.UnderConstruction };
            Contract = new Contract { Project = Project, BusinessPartner = Partner, ContractNumber = "FIN-SUM-C", Name = "财务汇总合同", TotalAmount = 100m };
            FirstLine = new ContractLineItem { Contract = Contract, Code = "001", Name = "清单一", Unit = "项", EstimatedQuantity = 1m, EstimatedUnitPrice = 60m };
            SecondLine = new ContractLineItem { Contract = Contract, Code = "002", Name = "清单二", Unit = "项", EstimatedQuantity = 1m, EstimatedUnitPrice = 40m };
            Contract.LineItems.Add(FirstLine);
            Contract.LineItems.Add(SecondLine);
            Project.Contracts.Add(Contract);
            Project.LegalEntities.Add(new ProjectLegalEntity { Project = Project, LegalEntity = LegalEntity, IsPrimary = true });
            OrdinaryTaxConfiguration = new ProjectTaxConfiguration { Project = Project, TaxRate = 0.03m, InvoiceType = ProjectInvoiceType.Ordinary };
            SpecialTaxConfiguration = new ProjectTaxConfiguration { Project = Project, TaxRate = 0.13m, InvoiceType = ProjectInvoiceType.Special };
            Project.TaxConfigurations.Add(OrdinaryTaxConfiguration);
            Project.TaxConfigurations.Add(SpecialTaxConfiguration);
            Db.AddRange(LegalEntity, Partner, Project);
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
