using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class CentralLedgerModelTests
{
    [Fact]
    public async Task CentralLedgerAggregateCanBePersistedInOneTransaction()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var legalEntity = new LegalEntity { Code = "LEDGER-LE", Name = "中央账本公司", ShortName = "账本公司" };
        var partner = new BusinessPartner { PartnerNumber = "LEDGER-BP", Name = "中央账本客户", ShortName = "账本客户" };
        var project = new Project { ProjectNumber = "LEDGER-P", Name = "中央账本项目", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract { Project = project, ContractNumber = "LEDGER-C", Name = "中央账本合同", TotalAmount = 1_000m };
        var lineItem = new ContractLineItem
        {
            Contract = contract,
            Code = "001",
            Name = "工程量",
            Unit = "项",
            EstimatedQuantity = 1m,
            EstimatedUnitPrice = 1_000m
        };
        contract.LineItems.Add(lineItem);
        project.Contracts.Add(contract);
        var account = new FinancialAccount
        {
            LegalEntity = legalEntity,
            AccountName = "基本户",
            AccountType = FinancialAccountType.Bank
        };
        var settlement = new FinanceSettlement
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            SettlementState = LedgerSettlementState.Final,
            SourceType = LedgerSourceType.ProjectQuantity,
            SourceId = lineItem.Id,
            LegalEntity = legalEntity,
            BusinessPartner = partner,
            Project = project,
            Contract = contract,
            ContractLineItem = lineItem,
            BusinessDate = new DateOnly(2026, 7, 19),
            OriginalAmount = 1_000m,
            OriginalInvoiceAmount = 1_000m,
            Notes = "项目工程量应收"
        };
        settlement.Adjustments.Add(new FinanceSettlementAdjustment
        {
            Settlement = settlement,
            AdjustmentType = LedgerAdjustmentType.FinalSettlement,
            AmountDelta = 100m,
            InvoiceAmountDelta = 100m,
            BusinessDate = new DateOnly(2026, 7, 20),
            Reason = "最终结算差额",
            ActorUserId = "model-test"
        });
        settlement.Deductions.Add(new FinanceDeduction
        {
            Settlement = settlement,
            BusinessDate = new DateOnly(2026, 7, 21),
            Amount = 50m,
            ReduceInvoiceAmount = true,
            Reason = "质量扣款"
        });
        var invoice = new FinanceInvoice
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            LegalEntity = legalEntity,
            BusinessPartner = partner,
            InvoiceNumber = "OUT-001",
            InvoiceDate = new DateOnly(2026, 7, 22),
            Amount = 600m
        };
        invoice.Allocations.Add(new FinanceInvoiceAllocation
        {
            Invoice = invoice,
            Settlement = settlement,
            Project = project,
            Contract = contract,
            ContractLineItem = lineItem,
            Amount = 600m,
            AllocationOrder = 1
        });
        var cash = new FinanceCashEntry
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            CashType = LedgerCashType.Collection,
            LegalEntity = legalEntity,
            BusinessPartner = partner,
            Account = account,
            BusinessDate = new DateOnly(2026, 7, 23),
            Amount = 500m
        };
        cash.Allocations.Add(new FinanceCashAllocation
        {
            CashEntry = cash,
            Settlement = settlement,
            Project = project,
            Contract = contract,
            ContractLineItem = lineItem,
            Amount = 500m,
            AllocationOrder = 1
        });
        var year = new FinanceBusinessYear
        {
            Name = "2026 财务年度",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        var reconciliation = new FinanceReconciliation
        {
            Scope = LedgerScope.External,
            ReconciliationScope = FinanceReconciliationScope.LegalEntity,
            FinanceBusinessYear = year,
            LegalEntity = legalEntity,
            AsOfDate = new DateOnly(2026, 7, 31),
            Version = 1,
            QueryJson = "{}",
            MetricsJson = "{}",
            CreatedByUserId = "model-test"
        };
        reconciliation.Lines.Add(new FinanceReconciliationLine
        {
            Reconciliation = reconciliation,
            SettlementId = settlement.Id,
            LegalEntityId = legalEntity.Id,
            BusinessPartnerId = partner.Id,
            ProjectId = project.Id,
            ContractId = contract.Id,
            SnapshotJson = "{}",
            MetricsJson = "{}"
        });
        var deletionLog = new FinanceDeletionLog
        {
            RecordType = FinanceRecordType.Deduction,
            RecordId = settlement.Deductions.Single().Id,
            DeletedByUserId = "model-test",
            EntryPoint = "model-test",
            Reason = "测试删除日志",
            SnapshotJson = "{}",
            BeforeMetricsJson = "{}",
            AfterMetricsJson = "{}"
        };
        var legacyMap = new FinanceLegacyMap
        {
            LegacyEntityType = "ReceivableEntry",
            LegacyId = Guid.NewGuid().ToString("D"),
            CentralRecordType = FinanceRecordType.Settlement,
            CentralRecordId = settlement.Id
        };

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.AddRange(legalEntity, partner, project, account, settlement, invoice, cash, year, reconciliation, deletionLog, legacyMap);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var savedSettlement = await db.FinanceSettlements
            .Include(item => item.Adjustments)
            .Include(item => item.Deductions)
            .SingleAsync();
        var savedInvoice = await db.FinanceInvoices.Include(item => item.Allocations).SingleAsync();
        var savedCash = await db.FinanceCashEntries.Include(item => item.Allocations).SingleAsync();

        savedSettlement.Scope.Should().Be(LedgerScope.External);
        savedSettlement.Direction.Should().Be(LedgerDirection.Receivable);
        savedSettlement.ProjectId.Should().NotBeNull();
        savedSettlement.Adjustments.Single().AmountDelta.Should().Be(100m);
        savedSettlement.Deductions.Single().ReduceInvoiceAmount.Should().BeTrue();
        savedInvoice.Allocations.Single().SettlementId.Should().Be(savedSettlement.Id);
        savedCash.Allocations.Single().SettlementId.Should().Be(savedSettlement.Id);
        (await db.FinanceBusinessYears.SingleAsync()).Name.Should().Be("2026 财务年度");
        (await db.FinanceReconciliationLines.SingleAsync()).SettlementId.Should().Be(savedSettlement.Id);
        (await db.FinanceDeletionLogs.SingleAsync()).Reason.Should().Be("测试删除日志");
        (await db.FinanceLegacyMaps.SingleAsync()).CentralRecordId.Should().Be(savedSettlement.Id);
    }

    [Fact]
    public async Task ExternalRecordRequiresBusinessPartner()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var legalEntity = new LegalEntity { Code = "EXT-LE", Name = "外部账公司", ShortName = "外部公司" };
        db.AddRange(legalEntity, new FinanceSettlement
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            SettlementState = LedgerSettlementState.Final,
            SourceType = LedgerSourceType.CentralLedger,
            LegalEntity = legalEntity,
            BusinessDate = new DateOnly(2026, 7, 19),
            OriginalAmount = 100m,
            OriginalInvoiceAmount = 100m
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task InternalRecordRequiresCounterLegalEntity()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var legalEntity = new LegalEntity { Code = "INT-LE", Name = "内部账公司", ShortName = "内部公司" };
        db.AddRange(legalEntity, new FinanceCashEntry
        {
            Scope = LedgerScope.Internal,
            Direction = LedgerDirection.Payable,
            CashType = LedgerCashType.InternalTransfer,
            LegalEntity = legalEntity,
            BusinessDate = new DateOnly(2026, 7, 19),
            Amount = 100m
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void SettlementDeletionCannotCascadeToInvoiceOrCashHeaders()
    {
        using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=:memory:").Options);

        var invoiceAllocation = db.Model.FindEntityType(typeof(FinanceInvoiceAllocation))!;
        var cashAllocation = db.Model.FindEntityType(typeof(FinanceCashAllocation))!;

        invoiceAllocation.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(FinanceSettlement)).DeleteBehavior
            .Should().Be(DeleteBehavior.Restrict);
        cashAllocation.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(FinanceSettlement)).DeleteBehavior
            .Should().Be(DeleteBehavior.Restrict);
        invoiceAllocation.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(FinanceInvoice)).DeleteBehavior
            .Should().Be(DeleteBehavior.Cascade);
        cashAllocation.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(FinanceCashEntry)).DeleteBehavior
            .Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public async Task FinanceBusinessYearRejectsAnInvertedDateRange()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        db.FinanceBusinessYears.Add(new FinanceBusinessYear
        {
            Name = "错误年度",
            StartDate = new DateOnly(2026, 12, 31),
            EndDate = new DateOnly(2026, 1, 1)
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    private static ApplicationDbContext CreateDb(SqliteConnection connection)
    {
        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
    }
}
