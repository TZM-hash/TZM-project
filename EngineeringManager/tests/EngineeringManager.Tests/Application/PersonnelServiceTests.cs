using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Personnel;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class PersonnelServiceTests
{
    [Fact]
    public async Task CreateInternalPersonBuildsOnePersonEmployeeAndCurrentEngagement()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();

        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-001", "内部人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            PositionTitle: "项目经理",
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "新增内部人员"), CancellationToken.None);

        created.EmployeeId.Should().NotBeNull();
        created.CurrentAffiliation!.Scope.Should().Be(PersonnelScope.Internal);
        (await fixture.Db.People.CountAsync()).Should().Be(1);
        (await fixture.Db.Employees.SingleAsync()).PersonId.Should().Be(created.Id);
        (await fixture.Db.PersonnelEngagementHistories.SingleAsync()).LegalEntityId.Should().Be(fixture.Company.Id);
    }

    [Fact]
    public async Task LaterAffiliationClosesOldRecordAndBecomesCurrentProject()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var oldProject = await fixture.AddProjectAsync("P-OLD", "旧项目");
        var newProject = await fixture.AddProjectAsync("P-NEW", "新项目");
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-002", "调动人员", PersonnelScope.Internal, EmployeeType.Labor, null,
            LegalEntityId: fixture.Company.Id,
            ProjectId: oldProject.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始归属"), CancellationToken.None);

        await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Labor, null,
            fixture.Company.Id, null, null, newProject.Id, null, "施工员",
            new DateOnly(2026, 6, 1), "调整项目"), CancellationToken.None);

        var details = await fixture.Service.GetAsync(created.Id, new DateOnly(2026, 8, 6), true, CancellationToken.None);
        var history = await fixture.Db.PersonnelEngagementHistories.OrderBy(item => item.StartDate).ToArrayAsync();
        details!.CurrentAffiliation!.ProjectId.Should().Be(newProject.Id);
        history.Should().HaveCount(2);
        history[0].EndDate.Should().Be(new DateOnly(2026, 5, 31));
    }

    [Fact]
    public async Task SwitchToExternalCrewCreatesWorkerMembershipAndKeepsEmployeeBridge()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-003", "转外人员", PersonnelScope.Internal, EmployeeType.Temporary, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);

        var switched = await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 7, 1), "转为班组人员"), CancellationToken.None);

        switched.EmployeeId.Should().NotBeNull();
        switched.ConstructionWorkerId.Should().NotBeNull();
        (await fixture.Db.ConstructionWorkers.SingleAsync()).PersonId.Should().Be(created.Id);
        (await fixture.Db.ConstructionCrewMemberships.SingleAsync()).CrewBusinessPartnerId.Should().Be(crew.Id);
        (await fixture.Db.PersonnelEngagementHistories.CountAsync()).Should().Be(2);
    }

    private sealed class PersonnelFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private PersonnelFixture(SqliteConnection connection, ApplicationDbContext db, IPersonnelService service, LegalEntity company)
        {
            this.connection = connection;
            Db = db;
            Service = service;
            Company = company;
        }

        public ApplicationDbContext Db { get; }
        public IPersonnelService Service { get; }
        public LegalEntity Company { get; }

        public static async Task<PersonnelFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var company = new LegalEntity { Code = "PERSONNEL-LE", Name = "人员测试公司", ShortName = "人员公司" };
            db.LegalEntities.Add(company);
            await db.SaveChangesAsync();
            return new PersonnelFixture(connection, db, new PersonnelService(db), company);
        }

        public async Task<Project> AddProjectAsync(string number, string name)
        {
            var project = new Project { ProjectNumber = number, Name = name, Stage = ProjectStage.UnderConstruction };
            project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntityId = Company.Id, IsPrimary = true });
            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project;
        }

        public async Task<BusinessPartner> AddCrewAsync()
        {
            var crew = new BusinessPartner { PartnerNumber = "CREW-001", Name = "人员测试班组", ShortName = "测试班组" };
            crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
            Db.BusinessPartners.Add(crew);
            await Db.SaveChangesAsync();
            return crew;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
