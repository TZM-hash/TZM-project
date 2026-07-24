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
    public async Task CompanyListReportsActiveAndTotalAccountCounts()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "LE-COUNT", Name = "账户统计公司", ShortName = "账户统计" };
        scope.Db.Add(company);
        scope.Db.FinancialAccounts.AddRange(
            new FinancialAccount { LegalEntity = company, AccountName = "启用账户", IsActive = true },
            new FinancialAccount { LegalEntity = company, AccountName = "停用账户", IsActive = false });
        await scope.Db.SaveChangesAsync();

        var listed = await scope.Service.ListAsync(CompanyActor.Administrator("admin"), default);
        var searched = await scope.Service.SearchAsync(CompanyActor.Administrator("admin"), "账户统计", default);

        listed.Should().ContainSingle(item => item.Id == company.Id && item.ActiveAccountCount == 1 && item.TotalAccountCount == 2);
        searched.Should().ContainSingle(item => item.Id == company.Id && item.ActiveAccountCount == 1 && item.TotalAccountCount == 2);
    }

    [Fact]
    public async Task CompanyAccountCanBeEditedAndDeactivatedUsingConcurrencyStamp()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "LE-EDIT", Name = "账户编辑公司", ShortName = "账户编辑" };
        scope.Db.Add(company);
        await scope.Db.SaveChangesAsync();
        var actor = CompanyActor.Administrator("admin");
        var created = await scope.Service.SaveAccountAsync(actor, new SaveCompanyAccountRequest(
            null, company.Id, "原账户", "1001", "原开户行", (int)FinancialAccountType.Bank, 10m,
            false, false, false, true, null, "新增账户"), default);

        var updated = await scope.Service.SaveAccountAsync(actor, new SaveCompanyAccountRequest(
            created.Id, company.Id, "修改后账户", "2002", "新开户行", (int)FinancialAccountType.Bank, 20m,
            false, false, false, false, created.ConcurrencyStamp, "删除账户"), default);
        var details = await scope.Service.GetAsync(actor, company.Id, default);

        created.ConcurrencyStamp.Should().NotBeEmpty();
        updated.ConcurrencyStamp.Should().NotBe(created.ConcurrencyStamp);
        details.Accounts.Should().ContainSingle(item => item.Id == created.Id && item.AccountName == "修改后账户" && !item.IsActive);
        (await scope.Db.AuditLogs.CountAsync(item => item.EntityType == nameof(FinancialAccount))).Should().Be(2);
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

    [Fact]
    public async Task DeactivatingAccountClearsDefaultFlags()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "LE-DEF", Name = "默认账户公司", ShortName = "默认" };
        scope.Db.Add(company);
        await scope.Db.SaveChangesAsync();
        var actor = CompanyActor.Administrator("admin");
        var created = await scope.Service.SaveAccountAsync(actor, new SaveCompanyAccountRequest(
            null, company.Id, "默认户", "1", "行", (int)FinancialAccountType.Bank, 0m,
            true, true, true, true, null, "新增"), default);

        var updated = await scope.Service.SaveAccountAsync(actor, new SaveCompanyAccountRequest(
            created.Id, company.Id, created.AccountName, "1", "行", (int)FinancialAccountType.Bank, 0m,
            true, true, true, false, created.ConcurrencyStamp, "停用公司账户"), default);

        updated.IsActive.Should().BeFalse();
        updated.IsDefaultCollection.Should().BeFalse();
        updated.IsDefaultPayment.Should().BeFalse();
        updated.IsDefaultInvoice.Should().BeFalse();
    }

    [Fact]
    public async Task WorkspaceQueriesReturnCompanyScopedRows()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "LE-WS", Name = "工作台公司", ShortName = "工作台" };
        var other = new LegalEntity { Code = "LE-OTHER", Name = "其他公司", ShortName = "其他" };
        var partner = new BusinessPartner { PartnerNumber = "BP-WS", Name = "客户甲", ShortName = "客户" };
        var project = new Project { ProjectNumber = "P-WS-01", Name = "工作台项目", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract
        {
            Project = project,
            ContractNumber = "C-WS-01",
            Name = "工作台合同",
            ContractType = ContractType.MainContract,
            TotalAmount = 1000m,
            LegalEntityAllocations = [new ContractLegalEntityAllocation { LegalEntity = company, Amount = 800m, Percentage = 80m }]
        };
        project.LegalEntities.Add(new ProjectLegalEntity { LegalEntity = company, IsPrimary = true });
        var bank = new FinancialAccount { LegalEntity = company, AccountName = "工作台户", AccountType = FinancialAccountType.Bank, IsActive = true };
        var inactive = new FinancialAccount { LegalEntity = company, AccountName = "停用户", AccountType = FinancialAccountType.Cash, IsActive = false };
        var receivable = new ReceivableEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, SourceType = ReceivableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 20), Amount = 600m };
        var collection = new CollectionEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, Account = bank, CollectionDate = new DateOnly(2026, 7, 21), Amount = 200m, Notes = "首笔收款" };
        var payable = new PayableEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, SourceType = PayableSourceType.Manual, EntryDate = new DateOnly(2026, 7, 20), Amount = 150m };
        var payment = new PaymentEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, Account = bank, PaymentDate = new DateOnly(2026, 7, 22), Amount = 50m, Notes = "首笔付款" };
        var invoice = new InvoiceEntry { Project = project, Contract = contract, LegalEntity = company, BusinessPartner = partner, Direction = InvoiceDirection.Output, InvoiceNumber = "INV-WS-01", InvoiceDate = new DateOnly(2026, 7, 23), GrossAmount = 300m, Status = InvoiceStatus.IssuedOrReceived };
        var otherBank = new FinancialAccount { LegalEntity = other, AccountName = "其他户", AccountType = FinancialAccountType.Bank, IsActive = true };
        var otherCollection = new CollectionEntry { Project = project, Contract = contract, LegalEntity = other, BusinessPartner = partner, Account = otherBank, CollectionDate = new DateOnly(2026, 7, 21), Amount = 999m };
        var certValid = new CompanyCertificate { LegalEntity = company, CertificateType = "营业执照", CertificateNumber = "CERT-1", ExpiresOn = new DateOnly(2027, 1, 1) };
        var certExpired = new CompanyCertificate { LegalEntity = company, CertificateType = "资质", CertificateNumber = "CERT-2", ExpiresOn = new DateOnly(2020, 1, 1) };
        scope.Db.AddRange(company, other, partner, project, contract, bank, inactive, otherBank, receivable, collection, payable, payment, invoice, otherCollection, certValid, certExpired);
        await scope.Db.SaveChangesAsync();

        var actor = CompanyActor.Administrator("admin");
        var summary = await scope.Service.GetWorkspaceSummaryAsync(actor, company.Id, default);
        summary.ProjectCount.Should().Be(1);
        summary.ContractCount.Should().Be(1);
        summary.ActiveAccountCount.Should().Be(1);
        summary.TotalAccountCount.Should().Be(2);
        summary.TotalCertificateCount.Should().Be(2);
        summary.ExpiredCertificateCount.Should().Be(1);
        summary.ValidCertificateCount.Should().Be(1);

        var projects = await scope.Service.ListCompanyProjectsAsync(actor, company.Id, null, 50, default);
        projects.Should().ContainSingle();
        projects[0].CompanyContractAmount.Should().Be(800m);
        projects[0].ReceivableAmount.Should().Be(600m);
        projects[0].CollectedAmount.Should().Be(200m);
        projects[0].PayableAmount.Should().Be(150m);
        projects[0].PaidAmount.Should().Be(50m);

        var collections = await scope.Service.ListCompanyCollectionsAsync(actor, company.Id, 50, default);
        collections.Should().ContainSingle(item => item.Amount == 200m);
        collections.Should().NotContain(item => item.Amount == 999m);

        var payments = await scope.Service.ListCompanyPaymentsAsync(actor, company.Id, 50, default);
        payments.Should().ContainSingle(item => item.Amount == 50m);

        var invoices = await scope.Service.ListCompanyInvoicesAsync(actor, company.Id, 50, default);
        invoices.Should().ContainSingle(item => item.InvoiceNumber == "INV-WS-01" && item.Direction == "销项");

        var activity = await scope.Service.ListRecentActivityAsync(actor, company.Id, 10, default);
        activity.Count.Should().BeLessThanOrEqualTo(10);
        activity.Should().NotBeEmpty();
        activity.Select(item => item.Date).Should().BeInDescendingOrder();
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
