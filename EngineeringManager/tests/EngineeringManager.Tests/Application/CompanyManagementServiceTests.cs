using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Companies;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Tests.Application;

public sealed class CompanyManagementServiceTests
{
    [Fact]
    public async Task CompanyAccountNotesRoundTripAndEnterAuditLog()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "LE-NOTES", Name = "备注公司", ShortName = "备注" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();

        var saved = await scope.Service.SaveAccountAsync(
            CompanyActor.Administrator("admin"),
            new SaveCompanyAccountRequest(
                null,
                company.Id,
                "备注账户",
                null,
                null,
                (int)FinancialAccountType.Bank,
                0m,
                false,
                false,
                false,
                true,
                null,
                "新增账户",
                "账户备注"),
            default);

        var details = await scope.Service.GetAsync(CompanyActor.Administrator("admin"), company.Id, default);
        saved.Notes.Should().Be("账户备注");
        details.Accounts.Should().ContainSingle(item => item.Id == saved.Id && item.Notes == "账户备注");
        var audit = await scope.Db.AuditLogs.SingleAsync(item => item.EntityType == nameof(FinancialAccount));
        using var after = JsonDocument.Parse(audit.AfterJson!);
        after.RootElement.GetProperty("Notes").GetString().Should().Be("账户备注");
    }

    [Fact]
    public async Task CompanyCanBeCreatedListedAndPreparedForCopyWithoutUniqueFields()
    {
        await using var scope = await CreateScopeAsync();
        var category = new CompanyCategory { Code = "GENERAL", Name = "一般纳税人有限公司" };
        scope.Db.CompanyCategories.Add(category);
        await scope.Db.SaveChangesAsync();
        var actor = CompanyActor.Administrator("admin");

        var company = await scope.Service.SaveCompanyAsync(actor, new SaveCompanyRequest(
            null, "LE-01", "测试工程有限公司", "测试工程", category.Id, "测试法人",
            "913TEST", "注册地址", "经营地址", "13800000000", "测试工程有限公司", "备注", null, "新增"), default);
        var copy = await scope.Service.PrepareCopyAsync(actor, company.Id, default);
        var items = await scope.Service.ListAsync(actor, default);

        items.Should().ContainSingle(item => item.Id == company.Id);
        copy.Id.Should().BeNull();
        copy.Code.Should().BeEmpty();
        copy.UnifiedSocialCreditCode.Should().BeNull();
        copy.Name.Should().Be("测试工程有限公司 - 副本");
    }

    [Fact]
    public async Task DashboardAggregatesCompanyBusinessAndExcludesAccountTransfersFromOperatingTotals()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "LE-02", Name = "统计公司", ShortName = "统计" };
        var partner = new BusinessPartner { PartnerNumber = "BP-01", Name = "测试总包", ShortName = "总包" };
        var project = new Project { ProjectNumber = "P-01", Name = "统计项目", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract { Project = project, ContractNumber = "C-01", Name = "施工合同", TotalAmount = 1000m };
        contract.LegalEntityAllocations.Add(new ContractLegalEntityAllocation { Contract = contract, LegalEntity = company, Amount = 1000m });
        project.Contracts.Add(contract);
        var bank = new FinancialAccount { LegalEntity = company, AccountName = "基本户", AccountType = FinancialAccountType.Bank, OpeningBalance = 20m };
        var cash = new FinancialAccount { LegalEntity = company, AccountName = "现金", AccountType = FinancialAccountType.Cash };
        var receivable = new ReceivableEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, SourceType = ReceivableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 17), Amount = 800m };
        var collection = new CollectionEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, Account = bank, CollectionDate = new DateOnly(2026, 7, 17), Amount = 500m };
        var payable = new PayableEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, SourceType = PayableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 17), Amount = 300m };
        var payment = new PaymentEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, Account = bank, PaymentDate = new DateOnly(2026, 7, 17), Amount = 100m };
        var invoice = new InvoiceEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, Direction = InvoiceDirection.Output, InvoiceNumber = "INV-01", InvoiceDate = new DateOnly(2026, 7, 17), GrossAmount = 400m, Status = InvoiceStatus.IssuedOrReceived };
        var transfer = new AccountTransfer { FromAccount = bank, ToAccount = cash, TransferDate = new DateOnly(2026, 7, 17), Amount = 50m };
        scope.Db.AddRange(company, partner, project, bank, cash, receivable, collection, payable, payment, invoice, transfer);
        await scope.Db.SaveChangesAsync();

        var dashboard = await scope.Service.GetDashboardAsync(CompanyActor.Administrator("admin"), company.Id, default);

        dashboard.ContractAmount.Should().Be(1000m);
        dashboard.ReceivableAmount.Should().Be(800m);
        dashboard.CollectedAmount.Should().Be(500m);
        dashboard.PayableAmount.Should().Be(300m);
        dashboard.PaidAmount.Should().Be(100m);
        dashboard.OutputInvoiceAmount.Should().Be(400m);
        dashboard.AccountBalance.Should().Be(20m);
    }

    [Fact]
    public async Task ActorOnlySeesAssignedCompanies()
    {
        await using var scope = await CreateScopeAsync();
        var first = new LegalEntity { Code = "LE-A", Name = "公司 A", ShortName = "A" };
        var second = new LegalEntity { Code = "LE-B", Name = "公司 B", ShortName = "B" };
        scope.Db.AddRange(first, second);
        await scope.Db.SaveChangesAsync();

        var items = await scope.Service.ListAsync(new CompanyActor("finance", false, false, [first.Id]), default);

        items.Should().ContainSingle(item => item.Id == first.Id);
    }

    [Fact]
    public async Task CompanySearchUsesAddressesAccountsAndCertificates()
    {
        await using var scope = await CreateScopeAsync();
        var category = new CompanyCategory { Code = "SEARCH-CAT", Name = "搜索分类" };
        var company = new LegalEntity { Code = "LE-SEARCH", Name = "全字段公司", ShortName = "全字段", CompanyCategory = category, LegalRepresentative = "搜索法人", RegisteredAddress = "搜索注册地址", Notes = "公司备注" };
        scope.Db.Add(company);
        await scope.Db.SaveChangesAsync();
        scope.Db.FinancialAccounts.Add(new FinancialAccount { LegalEntityId = company.Id, AccountName = "搜索账户", AccountNumber = "622200001234", BankName = "搜索银行", Notes = "账户备注" });
        scope.Db.CompanyCertificates.Add(new CompanyCertificate { LegalEntityId = company.Id, CertificateType = "搜索资质", CertificateNumber = "COMP-CERT-SEARCH", IssuingAuthority = "发证机关", Notes = "证书备注" });
        await scope.Db.SaveChangesAsync();

        (await scope.Service.SearchAsync(CompanyActor.Administrator("admin"), "搜索注册地址 搜索账户 搜索资质", CancellationToken.None)).Should().ContainSingle(item => item.Id == company.Id);
    }

    private static async Task<TestScope> CreateScopeAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return new TestScope(connection, db, new CompanyManagementService(db));
    }

    private sealed class TestScope(SqliteConnection connection, ApplicationDbContext db, CompanyManagementService service) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public CompanyManagementService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
