using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
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
    public async Task CreateBuildsUnifiedPersonAndInternalEngagement()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();

        var employee = await fixture.Service.CreateAsync(
            CreateRequest("E-PERSON", "统一员工") with
            {
                DefaultLegalEntityId = fixture.LegalEntity.Id,
                HireDate = new DateOnly(2026, 1, 1)
            },
            CancellationToken.None);

        var entity = await fixture.Db.Employees.Include(item => item.Person).SingleAsync(item => item.Id == employee.Id);
        entity.PersonId.Should().NotBeNull();
        entity.Person!.Name.Should().Be("统一员工");
        (await fixture.Db.PersonnelEngagementHistories.SingleAsync()).PersonId.Should().Be(entity.PersonId!.Value);
    }

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
    public async Task CreatingEmployeeForExistingExternalPersonRequiresUnifiedScopeSwitch()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        const string identityNumber = "110101199001010088";
        var crew = new BusinessPartner { PartnerNumber = "CREW-EXISTING", Name = "既有外部班组", ShortName = "既有班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var person = new Person
        {
            PersonNumber = "PER-EXTERNAL",
            Name = "既有外部人员",
            IdentityNumber = identityNumber,
            IdentityNumberNormalized = identityNumber
        };
        person.ConstructionWorker = new ConstructionWorker { Person = person, Name = person.Name, IdentityNumber = identityNumber };
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.External,
            ExternalType = ExternalPersonnelType.ConstructionCrew,
            BusinessPartner = crew,
            CrewBusinessPartner = crew,
            StartDate = new DateOnly(2026, 1, 1),
            IsPrimary = true,
            Reason = "既有外部身份"
        });
        fixture.Db.AddRange(crew, person);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.CreateAsync(
            CreateRequest("E-SCOPE-SWITCH", "既有外部人员") with { IdentityNumber = identityNumber },
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("该身份证号已存在外部人员档案，请在人员管理中使用身份切换。");
        (await fixture.Db.PersonnelEngagementHistories.CountAsync()).Should().Be(1);
        (await fixture.Db.Employees.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PrimaryAffiliationPeriodsCannotOverlap()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var employee = await fixture.Service.CreateAsync(CreateRequest("E-SVC-002", "归属员工"), CancellationToken.None);
        await fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(employee.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), fixture.Department.Id, null, null, fixture.LegalEntity.Id, "施工员", true, null),
            CancellationToken.None);

        (await fixture.Db.PersonnelEngagementHistories.OrderBy(item => item.StartDate).ToArrayAsync())
            .Should().Contain(item => item.StartDate == new DateOnly(2026, 1, 1)
                && item.EndDate == new DateOnly(2026, 3, 31)
                && item.Scope == PersonnelScope.Internal
                && item.OrganizationUnitId == fixture.Department.Id);

        var action = () => fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(employee.Id, new DateOnly(2026, 3, 1), null, fixture.Department.Id, null, null, fixture.LegalEntity.Id, "施工员", true, null),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*重叠*");
    }

    [Fact]
    public async Task LaterPrimaryAffiliationPreservesEarlierInternalEngagementHistory()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var employee = await fixture.Service.CreateAsync(
            CreateRequest("E-HISTORY-SPLIT", "历史保留员工") with { HireDate = new DateOnly(2026, 1, 1) },
            CancellationToken.None);

        await fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(employee.Id, new DateOnly(2026, 3, 1), null, fixture.Department.Id, null, null, fixture.LegalEntity.Id, "施工员", true, null),
            CancellationToken.None);

        var engagements = await fixture.Db.PersonnelEngagementHistories
            .OrderBy(item => item.StartDate)
            .ToArrayAsync();
        engagements.Should().HaveCount(2);
        engagements[0].StartDate.Should().Be(new DateOnly(2026, 1, 1));
        engagements[0].EndDate.Should().Be(new DateOnly(2026, 2, 28));
        engagements[0].Reason.Should().Be("员工档案创建");
        engagements[1].StartDate.Should().Be(new DateOnly(2026, 3, 1));
        engagements[1].EndDate.Should().BeNull();
        engagements[1].OrganizationUnitId.Should().Be(fixture.Department.Id);
        engagements[1].Reason.Should().Be("员工业务档案归属维护");
    }

    [Fact]
    public async Task LaterPrimaryAffiliationSplitsFiniteInitialEngagementHistory()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var employee = await fixture.Service.CreateAsync(
            CreateRequest("E-HISTORY-FINITE", "有限期历史员工") with
            {
                HireDate = new DateOnly(2026, 1, 1),
                LeaveDate = new DateOnly(2026, 12, 31)
            },
            CancellationToken.None);

        await fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(employee.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 6, 30), fixture.Department.Id, null, null, fixture.LegalEntity.Id, "施工员", true, null),
            CancellationToken.None);

        var engagements = await fixture.Db.PersonnelEngagementHistories
            .OrderBy(item => item.StartDate)
            .ToArrayAsync();
        engagements.Should().HaveCount(2);
        engagements[0].StartDate.Should().Be(new DateOnly(2026, 1, 1));
        engagements[0].EndDate.Should().Be(new DateOnly(2026, 2, 28));
        engagements[1].StartDate.Should().Be(new DateOnly(2026, 3, 1));
        engagements[1].EndDate.Should().Be(new DateOnly(2026, 6, 30));
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
        updated.EmployeeType.Should().Be(EmployeeType.Formal);
        updated.Notes.Should().Be("员工主档备注");
        var engagement = await fixture.Db.PersonnelEngagementHistories.SingleAsync();
        engagement.InternalType.Should().Be(EmployeeType.Formal);
        engagement.PositionTitle.Should().BeNull();
        (await fixture.Db.AuditLogs.SingleAsync()).Action.Should().Be("UpdateEmployee");
        using var auditJson = JsonDocument.Parse((await fixture.Db.AuditLogs.SingleAsync()).AfterJson!);
        auditJson.RootElement.GetProperty("Notes").GetString().Should().Be("员工主档备注");
    }

    [Fact]
    public async Task UpdatingHistoricalEmployeeProfilePreservesCurrentExternalIdentityActivity()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var crew = new BusinessPartner { PartnerNumber = "CREW-HISTORY", Name = "历史身份班组", ShortName = "历史班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var person = new Person { PersonNumber = "PER-HISTORY", Name = "历史员工", IsActive = true };
        var employee = new Employee
        {
            Person = person,
            EmployeeNumber = "E-HISTORY",
            Name = person.Name,
            EmployeeType = EmployeeType.Formal,
            IsActive = false
        };
        var worker = new ConstructionWorker { Person = person, Name = person.Name, IsActive = true };
        person.Employee = employee;
        person.ConstructionWorker = worker;
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.External,
            ExternalType = ExternalPersonnelType.ConstructionCrew,
            BusinessPartner = crew,
            CrewBusinessPartner = crew,
            StartDate = new DateOnly(2026, 1, 1),
            IsPrimary = true,
            Reason = "当前外部身份"
        });
        fixture.Db.AddRange(crew, person);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateEmployeeRequest(
                employee.Id,
                employee.EmployeeNumber,
                "更新后的公共姓名",
                EmployeeType.Labor,
                "13800000000",
                null,
                null,
                null,
                null,
                null,
                "不应覆盖当前外部岗位",
                null,
                null,
                320m,
                null,
                null,
                true,
                employee.ConcurrencyStamp,
                "维护历史员工业务档案",
                null),
            CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.People.SingleAsync()).IsActive.Should().BeTrue();
        (await fixture.Db.Employees.SingleAsync()).IsActive.Should().BeFalse();
        (await fixture.Db.ConstructionWorkers.SingleAsync()).IsActive.Should().BeTrue();
        (await fixture.Db.PersonnelEngagementHistories.SingleAsync()).Scope.Should().Be(PersonnelScope.External);
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

    [Fact]
    public async Task EmployeeSearchUsesAllFieldsAndRequiresEveryKeyword()
    {
        await using var fixture = await EmployeeFixture.CreateAsync();
        var project = new Project { ProjectNumber = "EMP-SEARCH-P", Name = "全字段项目" };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        var employee = await fixture.Service.CreateAsync(
            CreateRequest("E-SEARCH", "搜索员工") with
            {
                Phone = "13800138000",
                PositionTitle = "安全员",
                Notes = "夜班备注",
                IdentityNumber = "110101199001010011"
            }, CancellationToken.None);
        await fixture.Service.AddAffiliationAsync(
            new CreateEmployeeAffiliationRequest(employee.Id, new DateOnly(2026, 1, 1), null, fixture.Department.Id, project.Id, null, fixture.LegalEntity.Id, "安全员", true, "归属备注"),
            CancellationToken.None);
        fixture.Db.EmployeeCertificates.Add(new EmployeeCertificate { EmployeeId = employee.Id, CertificateType = "安全生产证", CertificateNumber = "CERT-SEARCH", IssuingAuthority = "住建局" });
        await fixture.Db.SaveChangesAsync();

        (await fixture.Service.ListAsync("13800138000 安全生产证 全字段项目", false, CancellationToken.None)).Should().ContainSingle(item => item.Id == employee.Id);
        (await fixture.Service.ListAsync("13800138000 不存在的词", false, CancellationToken.None)).Should().BeEmpty();
        (await fixture.Service.ListAsync("110101199001010011", false, CancellationToken.None)).Should().BeEmpty();
        (await fixture.Service.ListAsync("110101199001010011", true, CancellationToken.None)).Should().ContainSingle(item => item.Id == employee.Id);
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
