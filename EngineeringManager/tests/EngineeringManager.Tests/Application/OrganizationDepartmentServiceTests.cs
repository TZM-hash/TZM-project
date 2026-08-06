using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Organization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class OrganizationDepartmentServiceTests
{
    [Fact]
    public async Task SameOrganizationCannotReuseADepartmentCode()
    {
        await using var fixture = await Fixture.CreateAsync();
        var owner = await fixture.AddLegalEntityAsync("LE-01", "甲公司");
        await fixture.Service.SaveDepartmentAsync(new SaveDepartmentRequest(
            null, OrganizationOwnerKind.LegalEntity, owner.Id, "GC", "工程部", null, true, true), CancellationToken.None);

        var action = () => fixture.Service.SaveDepartmentAsync(new SaveDepartmentRequest(
            null, OrganizationOwnerKind.LegalEntity, owner.Id, " gc ", "另一个工程部", null, true, true), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*部门编码*");
    }

    [Fact]
    public async Task DifferentOrganizationsMayReuseTheSameDepartmentCode()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.AddLegalEntityAsync("LE-01", "甲公司");
        var second = await fixture.AddLegalEntityAsync("LE-02", "乙公司");

        var firstDepartment = await fixture.Service.SaveDepartmentAsync(new SaveDepartmentRequest(
            null, OrganizationOwnerKind.LegalEntity, first.Id, "GC", "工程部", null, true, true), CancellationToken.None);
        var secondDepartment = await fixture.Service.SaveDepartmentAsync(new SaveDepartmentRequest(
            null, OrganizationOwnerKind.LegalEntity, second.Id, "GC", "工程部", null, true, true), CancellationToken.None);

        firstDepartment.Id.Should().NotBe(secondDepartment.Id);
    }

    [Fact]
    public async Task ParentDepartmentMustBelongToTheSameOrganization()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.AddLegalEntityAsync("LE-01", "甲公司");
        var second = await fixture.AddLegalEntityAsync("LE-02", "乙公司");
        var parent = await fixture.Service.SaveDepartmentAsync(new SaveDepartmentRequest(
            null, OrganizationOwnerKind.LegalEntity, first.Id, "GC", "工程部", null, true, true), CancellationToken.None);

        var action = () => fixture.Service.SaveDepartmentAsync(new SaveDepartmentRequest(
            null, OrganizationOwnerKind.LegalEntity, second.Id, "XM", "项目部", parent.Id, true, true), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*同一组织*");
    }

    [Fact]
    public async Task CurrentPersonnelReferencePreventsDepartmentDeactivation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var owner = await fixture.AddLegalEntityAsync("LE-01", "甲公司");
        var department = await fixture.Service.SaveDepartmentAsync(new SaveDepartmentRequest(
            null, OrganizationOwnerKind.LegalEntity, owner.Id, "GC", "工程部", null, true, true), CancellationToken.None);
        var person = new Person { PersonNumber = "RY0001", Name = "在岗人员" };
        fixture.Db.People.Add(person);
        fixture.Db.PersonnelEngagementHistories.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.Internal,
            InternalType = EmployeeType.Formal,
            LegalEntityId = owner.Id,
            OrganizationUnitId = department.Id,
            StartDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-10),
            IsPrimary = true,
            Reason = "测试在岗归属"
        });
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.DeactivateDepartmentAsync(department.Id, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*当前人员*");
        (await fixture.Db.OrganizationUnits.FindAsync(department.Id))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DepartmentListIncludesCurrentPersonnelCountAndOwnerIdentity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var owner = await fixture.AddBusinessPartnerAsync("BP-01", "合作单位甲");
        var department = await fixture.Service.SaveDepartmentAsync(new SaveDepartmentRequest(
            null, OrganizationOwnerKind.BusinessPartner, owner.Id, "GC", "工程部", null, false, true), CancellationToken.None);
        var person = new Person { PersonNumber = "RY0002", Name = "外部人员" };
        fixture.Db.People.Add(person);
        fixture.Db.PersonnelEngagementHistories.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.External,
            ExternalType = ExternalPersonnelType.BusinessPartner,
            BusinessPartnerId = owner.Id,
            OrganizationUnitId = department.Id,
            StartDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1),
            IsPrimary = true,
            Reason = "测试外部归属"
        });
        await fixture.Db.SaveChangesAsync();

        var rows = await fixture.Service.ListDepartmentsAsync(
            OrganizationOwnerKind.BusinessPartner, owner.Id, true, CancellationToken.None);

        rows.Should().ContainSingle();
        rows[0].OwnerName.Should().Be("合作单位甲");
        rows[0].CurrentPersonnelCount.Should().Be(1);
        rows[0].IsAuthorizationScope.Should().BeFalse();
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public OrganizationService Service { get; } = new(db);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async Task<LegalEntity> AddLegalEntityAsync(string code, string name)
        {
            var entity = new LegalEntity { Code = code, Name = name, ShortName = name };
            Db.LegalEntities.Add(entity);
            await Db.SaveChangesAsync();
            return entity;
        }

        public async Task<BusinessPartner> AddBusinessPartnerAsync(string number, string name)
        {
            var partner = new BusinessPartner { PartnerNumber = number, Name = name, ShortName = name };
            Db.BusinessPartners.Add(partner);
            await Db.SaveChangesAsync();
            return partner;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
