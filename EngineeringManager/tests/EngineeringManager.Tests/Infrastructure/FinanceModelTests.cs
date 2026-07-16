using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class FinanceModelTests
{
    [Fact]
    public async Task FinanceDocumentsInvoicesAccountsAndTransfersCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var legalEntity = new LegalEntity { Code = "FIN-LE", Name = "财务测试公司", ShortName = "财务公司" };
        var partner = new BusinessPartner { PartnerNumber = "FIN-BP", Name = "财务合作单位", ShortName = "财务单位" };
        var project = new Project { ProjectNumber = "FIN-P", Name = "财务测试项目", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract { Project = project, ContractNumber = "FIN-C", Name = "财务测试合同", TotalAmount = 100m };
        var lineItem = new ContractLineItem { Contract = contract, Code = "001", Name = "财务清单", Unit = "项", EstimatedQuantity = 1m, EstimatedUnitPrice = 100m };
        contract.LineItems.Add(lineItem);
        project.Contracts.Add(contract);
        var bank = new FinancialAccount { LegalEntity = legalEntity, AccountName = "基本户", AccountType = FinancialAccountType.Bank };
        var cash = new FinancialAccount { LegalEntity = legalEntity, AccountName = "现金", AccountType = FinancialAccountType.Cash };
        var receivable = new ReceivableEntry { Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, SourceType = ReceivableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 16), Amount = 100m };
        var collection = new CollectionEntry { Receivable = receivable, Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, Account = bank, CollectionDate = new DateOnly(2026, 7, 16), Amount = 60m };
        var refund = new RefundOrReversalEntry { Collection = collection, Receivable = receivable, Account = bank, EntryDate = new DateOnly(2026, 7, 16), Amount = 10m, AdjustmentType = FinancialAdjustmentType.Refund, Reason = "退款" };
        var payable = new PayableEntry { Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, SourceType = PayableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 16), Amount = 80m };
        var payment = new PaymentEntry { Payable = payable, Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, Account = bank, PaymentDate = new DateOnly(2026, 7, 16), Amount = 50m };
        var deduction = new DeductionEntry { Payable = payable, Project = project, LegalEntity = legalEntity, BusinessPartner = partner, EntryDate = new DateOnly(2026, 7, 16), Amount = 5m, Reason = "质量扣款" };
        var invoice = new InvoiceEntry { Project = project, Contract = contract, LegalEntity = legalEntity, BusinessPartner = partner, Direction = InvoiceDirection.Output, InvoiceNumber = "INV-001", InvoiceDate = new DateOnly(2026, 7, 16), TaxRate = 13m, NetAmount = 50m, TaxAmount = 6.5m, GrossAmount = 56.5m, Status = InvoiceStatus.IssuedOrReceived };
        invoice.ReceivableLinks.Add(new InvoiceReceivableLink { Invoice = invoice, Receivable = receivable, AllocatedAmount = 56.5m });
        invoice.LineItemLinks.Add(new InvoiceLineItemLink { Invoice = invoice, ContractLineItem = lineItem, AllocatedAmount = 56.5m });
        var transfer = new AccountTransfer { FromAccount = bank, ToAccount = cash, TransferDate = new DateOnly(2026, 7, 16), Amount = 20m, Description = "备用金" };

        db.AddRange(legalEntity, partner, project, bank, cash, receivable, collection, refund, payable, payment, deduction, invoice, transfer);
        await db.SaveChangesAsync();

        (await db.ReceivableEntries.SingleAsync()).Amount.Should().Be(100m);
        (await db.InvoiceReceivableLinks.SingleAsync()).AllocatedAmount.Should().Be(56.5m);
        (await db.AccountTransfers.SingleAsync()).Amount.Should().Be(20m);
    }
}
