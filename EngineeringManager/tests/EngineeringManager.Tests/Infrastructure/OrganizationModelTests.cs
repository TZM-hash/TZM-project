using EngineeringManager.Domain.Organization;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class OrganizationModelTests
{
    [Fact]
    public async Task OrganizationHierarchyAndUserAccessCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var branch = new OrganizationUnit { Code = "BR-01", Name = "华东分支", UnitType = OrganizationUnitType.Branch };
        var department = new OrganizationUnit { Code = "DEP-01", Name = "工程部", UnitType = OrganizationUnitType.Department, Parent = branch };
        var legalEntity = new LegalEntity { Code = "LE-01", Name = "测试工程有限公司", ShortName = "测试工程" };
        var user = new ApplicationUser { UserName = "manager", NormalizedUserName = "MANAGER", DisplayName = "项目管理员" };

        db.AddRange(branch, department, legalEntity, user);
        db.UserOrganizationMemberships.Add(new UserOrganizationMembership
        {
            User = user,
            OrganizationUnit = department,
            IsPrimary = true
        });
        db.UserLegalEntityAccesses.Add(new UserLegalEntityAccess
        {
            User = user,
            LegalEntity = legalEntity,
            IsDefault = true
        });
        await db.SaveChangesAsync();

        (await db.OrganizationUnits.CountAsync()).Should().Be(2);
        (await db.UserOrganizationMemberships.SingleAsync()).IsPrimary.Should().BeTrue();
        (await db.UserLegalEntityAccesses.SingleAsync()).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task OrganizationAndLegalEntityCodesAreUnique()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.OrganizationUnits.AddRange(
            new OrganizationUnit { Code = "DUP", Name = "部门一", UnitType = OrganizationUnitType.Department },
            new OrganizationUnit { Code = "DUP", Name = "部门二", UnitType = OrganizationUnitType.Department });

        var action = () => db.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
}
