using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class CompanyModelTests
{
    [Fact]
    public async Task CompanyCategoryCertificateAndDefaultAccountsCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var category = new CompanyCategory { Code = "TEST_CUSTOM", Name = "测试自定义主体" };
        var company = new LegalEntity
        {
            Code = "COMP-01",
            Name = "测试设备经营部",
            ShortName = "测试经营部",
            CompanyCategory = category,
            LegalRepresentative = "测试经营者"
        };
        var account = new FinancialAccount
        {
            LegalEntity = company,
            AccountName = "默认账户",
            AccountType = FinancialAccountType.Bank,
            IsDefaultCollection = true,
            IsDefaultPayment = true,
            IsDefaultInvoice = true
        };
        var certificate = new CompanyCertificate
        {
            LegalEntity = company,
            CertificateType = "营业执照",
            CertificateNumber = "TEST-LICENSE",
            ExpiresOn = new DateOnly(2030, 12, 31)
        };

        db.AddRange(category, company, account, certificate);
        await db.SaveChangesAsync();

        (await db.CompanyCategories.SingleAsync(item => item.Code == "TEST_CUSTOM")).Name.Should().Be("测试自定义主体");
        (await db.CompanyCertificates.SingleAsync()).ExpiresOn.Should().Be(new DateOnly(2030, 12, 31));
        (await db.FinancialAccounts.SingleAsync()).IsDefaultInvoice.Should().BeTrue();
    }

    [Fact]
    public async Task CompanyCategoryCodesAndDefaultCollectionAccountsAreUnique()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var company = new LegalEntity { Code = "COMP-02", Name = "测试公司", ShortName = "测试" };
        db.CompanyCategories.AddRange(
            new CompanyCategory { Code = "DUP", Name = "分类一" },
            new CompanyCategory { Code = "DUP", Name = "分类二" });
        db.FinancialAccounts.AddRange(
            new FinancialAccount { LegalEntity = company, AccountName = "账户一", AccountType = FinancialAccountType.Bank, IsDefaultCollection = true },
            new FinancialAccount { LegalEntity = company, AccountName = "账户二", AccountType = FinancialAccountType.Bank, IsDefaultCollection = true });

        var action = () => db.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
}
