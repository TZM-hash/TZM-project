using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Employees;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Tests.Application;

public sealed class EmployeeServiceTests
{
    [Fact]
    public async Task DuplicateEmployeeNumberIsRejected()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var request = CreateRequest("E-SVC-001", "员工一");
        await fixture.Service.CreateAsync(request, CancellationToken.None);

        var action = () => fixture.Service.CreateAsync(request with { Name = "员工二" }, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*员工编号*");
    }

    [Fact]
    public async Task PrimaryAffiliationPeriodsCannotOverlap()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var employee = await fixture.Service.CreateAsync(CreateRequest("E-SVC-002", "归属员工"), CancellationToken.None);
        await fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(employee.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), fixture.Department.Id, null, null, fixture.LegalEntity.Id, "施工员", true, null),
            CancellationToken.None);

        var action = () => fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(employee.Id, new DateOnly(2026, 3, 1), null, fixture.Department.Id, null, null, fixture.LegalEntity.Id, "施工员", true, null),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*重叠*");
    }

    [Fact]
    public async Task CopyPreservesWorkDefaultsButClearsSensitiveAndHistoricalData()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var source = await fixture.Service.CreateAsync(
            CreateRequest("E-SVC-003", "源员工") with
            {
                EmployeeType = EmployeeType.Labor,
                Phone = "13800000000",
                IdentityNumber = "110101199001010011",
                BankAccountNumber = "622200001",
                PositionTitle = "焊工",
                DefaultDailyRate = 380m,
                DefaultPieceworkRate = 25m
            },
            CancellationToken.None);
        await fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(source.Id, new DateOnly(2026, 1, 1), null, fixture.Department.Id, null, null, fixture.LegalEntity.Id, "焊工", true, null),
            CancellationToken.None);

        var copy = await fixture.Service.CopyAsync(new CopyEmployeeRequest(source.Id, "E-SVC-004", "复制员工"), CancellationToken.None);
        var copiedEntity = await fixture.Db.Employees.Include(item => item.AffiliationHistory).SingleAsync(item => item.Id == copy.Id);

        copiedEntity.EmployeeType.Should().Be(EmployeeType.Labor);
        copiedEntity.PositionTitle.Should().Be("焊工");
        copiedEntity.DefaultDailyRate.Should().Be(380m);
        copiedEntity.DefaultPieceworkRate.Should().Be(25m);
        copiedEntity.Phone.Should().BeNull();
        copiedEntity.IdentityNumber.Should().BeNull();
        copiedEntity.BankAccountNumber.Should().BeNull();
        copiedEntity.AffiliationHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateChangesEmployeeAndWritesAuditLog()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var employee = await fixture.Service.CreateAsync(CreateRequest("E-SVC-005", "待修改员工"), CancellationToken.None);

        var updated = await fixture.Service.UpdateAsync("admin", new UpdateEmployeeRequest(employee.Id, employee.EmployeeNumber, "已修改员工", EmployeeType.Labor, "13800000001", null, null, null, null, null, "操作员", null, null, 320m, null, null, true, employee.ConcurrencyStamp, "调整员工主档", "员工主档备注"), CancellationToken.None);

        updated.Name.Should().Be("已修改员工");
        updated.EmployeeType.Should().Be(EmployeeType.Labor);
        updated.Notes.Should().Be("员工主档备注");
        (await fixture.Db.AuditLogs.SingleAsync()).Action.Should().Be("UpdateEmployee");
        using var auditJson = JsonDocument.Parse((await fixture.Db.AuditLogs.SingleAsync()).AfterJson!);
        auditJson.RootElement.GetProperty("Notes").GetString().Should().Be("员工主档备注");
    }

    [Fact]
    public async Task EmployeeDetailsReturnAffiliationDisplayNames()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var project = new Project { ProjectNumber = "EMP-SVC-P", Name = "员工服务项目", Stage = ProjectStage.UnderConstruction };
        var crew = new BusinessPartner { PartnerNumber = "EMP-SVC-C", Name = "员工服务班组", ShortName = "服务班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        fixture.Db.AddRange(project, crew);
        await fixture.Db.SaveChangesAsync();
        var employee = await fixture.Service.CreateAsync(CreateRequest("E-SVC-006", "详情员工"), CancellationToken.None);
        await fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(employee.Id, new DateOnly(2026, 1, 1), null, fixture.Department.Id, project.Id, crew.Id, fixture.LegalEntity.Id, "施工员", true, null),
            CancellationToken.None);

        var details = await fixture.Service.GetAsync(employee.Id, CancellationToken.None);
        var affiliation = details!.Affiliations.Single();

        affiliation.DepartmentName.Should().Be(fixture.Department.Name);
        affiliation.ProjectName.Should().Be(project.Name);
        affiliation.CrewBusinessPartnerName.Should().Be(crew.Name);
        affiliation.LegalEntityName.Should().Be(fixture.LegalEntity.ShortName);
    }

    private static CreateEmployeeRequest CreateRequest(string number, string name) =>
        new(number, name, EmployeeType.Formal, null, null, null, null, null, null, null, null, null, null, true);

    private sealed class EmployeeFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private EmployeeFixture(SqliteConnection connection, ApplicationDbContext db, IEmployeeService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IEmployeeService Service { get; }
        public OrganizationUnit Department { get; private set; } = null!;
        public LegalEntity LegalEntity { get; private set; } = null!;

        public static async Task<EmployeeFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new EmployeeFixture(connection, db, new EmployeeService(db));
            fixture.Department = new OrganizationUnit { Code = "EMP-SVC-DEPT", Name = "员工服务部门", UnitType = OrganizationUnitType.Department };
            fixture.LegalEntity = new LegalEntity { Code = "EMP-SVC-LE", Name = "员工服务公司", ShortName = "服务公司" };
            db.AddRange(fixture.Department, fixture.LegalEntity);
            await db.SaveChangesAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
