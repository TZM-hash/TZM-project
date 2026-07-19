using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Infrastructure.Finance;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectWorkbookCentralLedgerTests
{
    [Fact]
    public async Task FinanceSheetsExportCentralIdsStatesMetricsAllocationsAndDeductionOption()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var company = new LegalEntity { Code = "WB-CENTRAL-LE", Name = "工作簿中央公司", ShortName = "中央公司" };
        var partner = new BusinessPartner { PartnerNumber = "WB-CENTRAL-BP", Name = "工作簿中央客户", ShortName = "中央客户" };
        var project = new Project { ProjectNumber = "WB-CENTRAL-P", Name = "工作簿中央项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var contract = new Contract { Project = project, BusinessPartner = partner, ContractNumber = "WB-CENTRAL-C", Name = "中央合同", TotalAmount = 1_000m };
        project.Contracts.Add(contract);
        var account = new FinancialAccount { LegalEntity = company, AccountName = "中央账户", AccountType = FinancialAccountType.Bank };
        var settlement = new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = LedgerSettlementState.Provisional,
            SourceType = LedgerSourceType.CentralLedger, LegalEntity = company, BusinessPartner = partner, Project = project,
            Contract = contract, BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 1_000m,
            OriginalInvoiceAmount = 900m, Notes = "中央应收"
        };
        settlement.Deductions.Add(new FinanceDeduction { Settlement = settlement, BusinessDate = new DateOnly(2026, 7, 2), Amount = 100m, ReduceInvoiceAmount = true, Reason = "质保扣款" });
        var invoice = new FinanceInvoice { Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, LegalEntity = company, BusinessPartner = partner, InvoiceNumber = "WB-INV-001", InvoiceDate = new DateOnly(2026, 7, 3), Amount = 400m };
        invoice.Allocations.Add(new FinanceInvoiceAllocation { Invoice = invoice, Settlement = settlement, Project = project, Contract = contract, Amount = 400m, AllocationOrder = 1 });
        var cash = new FinanceCashEntry { Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, CashType = LedgerCashType.Collection, LegalEntity = company, BusinessPartner = partner, Account = account, BusinessDate = new DateOnly(2026, 7, 4), Amount = 300m };
        cash.Allocations.Add(new FinanceCashAllocation { CashEntry = cash, Settlement = settlement, Project = project, Contract = contract, Amount = 300m, AllocationOrder = 1 });
        db.AddRange(company, partner, project, account, settlement, invoice, cash);
        await db.SaveChangesAsync();
        var service = new ProjectWorkbookService(db, new ProjectService(db), new FinanceLedgerService(db));

        var file = await service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(new ProjectListActor("admin", true), new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false), false, [project.Id]),
            [ProjectWorkbookSheet.Receivables, ProjectWorkbookSheet.Collections, ProjectWorkbookSheet.Invoices, ProjectWorkbookSheet.Deductions],
            Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        var sheets = SimpleXlsxReader.Read(file.Content);

        var receivables = sheets.Single(item => item.Name == "应收");
        receivables.Rows[0].Should().Contain(["结算状态", "原始金额", "实际金额", "原始应开票金额", "当前应开票金额", "发票分摊状态", "资金分摊状态"]);
        receivables.Rows.SelectMany(item => item).Should().Contain(settlement.Id.ToString()).And.Contain("Provisional");
        var deductions = sheets.Single(item => item.Name == "扣款");
        deductions.Rows[0].Should().Contain("同时扣减应开票金额");
        deductions.Rows.SelectMany(item => item).Should().Contain(true);
        sheets.Single(item => item.Name == "收款").Rows.SelectMany(item => item).Should().Contain(cash.Id.ToString());
        sheets.Single(item => item.Name == "发票").Rows.SelectMany(item => item).Should().Contain(invoice.Id.ToString());
    }
}
