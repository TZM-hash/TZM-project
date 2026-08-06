using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Organization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class OrganizationSummaryServiceTests
{
    [Fact]
    public async Task CompanySummaryCountsEveryActiveProjectStageAndDepartment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var company = new LegalEntity { Code = "LE-SUM", Name = "汇总公司", ShortName = "汇总公司" };
        fixture.Db.LegalEntities.Add(company);
        foreach (var stage in Enum.GetValues<ProjectStage>())
        {
            var project = new Project { ProjectNumber = $"P-{(int)stage}", Name = stage.ToString(), Stage = stage };
            project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
            fixture.Db.Projects.Add(project);
        }
        var inactive = new Project { ProjectNumber = "P-INACTIVE", Name = "停用项目", Stage = ProjectStage.UnderConstruction, IsActive = false };
        inactive.LegalEntities.Add(new ProjectLegalEntity { Project = inactive, LegalEntity = company, IsPrimary = true });
        fixture.Db.Projects.Add(inactive);
        fixture.Db.OrganizationUnits.AddRange(
            new OrganizationUnit { Code = "A", Name = "启用部门", UnitType = OrganizationUnitType.Department, LegalEntityId = company.Id, IsActive = true },
            new OrganizationUnit { Code = "B", Name = "停用部门", UnitType = OrganizationUnitType.Department, LegalEntityId = company.Id, IsActive = false });
        await fixture.Db.SaveChangesAsync();

        var summary = await fixture.Service.GetAsync(
            new OrganizationSummaryQuery(OrganizationOwnerKind.LegalEntity, company.Id, Today()), CancellationToken.None);

        summary.Projects.TotalCount.Should().Be(6);
        summary.Projects.InProgressCount.Should().Be(2);
        summary.Projects.SuspendedCount.Should().Be(1);
        summary.Projects.CompletedUnsettledCount.Should().Be(1);
        summary.Projects.PartiallySettledCount.Should().Be(1);
        summary.Projects.SettledArchivedCount.Should().Be(1);
        summary.Departments.TotalCount.Should().Be(2);
        summary.Departments.ActiveCount.Should().Be(1);
    }

    [Fact]
    public async Task CrewProjectSummaryUnionsPartnerAndConstructionLinksWithoutDuplicates()
    {
        await using var fixture = await Fixture.CreateAsync();
        var crew = new BusinessPartner { PartnerNumber = "CREW-01", Name = "一班组", ShortName = "一班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var linkedAndConstructed = new Project { ProjectNumber = "P-BOTH", Name = "双重关联", Stage = ProjectStage.UnderConstruction };
        linkedAndConstructed.Partners.Add(new ProjectPartner { Project = linkedAndConstructed, Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        linkedAndConstructed.ConstructionRecords.Add(new ProjectConstructionRecord { Project = linkedAndConstructed, CrewBusinessPartner = crew, RecordType = ProjectConstructionRecordType.ConstructionCrew });
        var constructionOnly = new Project { ProjectNumber = "P-CONSTRUCTION", Name = "施工记录关联", Stage = ProjectStage.SettledArchived };
        constructionOnly.ConstructionRecords.Add(new ProjectConstructionRecord { Project = constructionOnly, CrewBusinessPartner = crew, RecordType = ProjectConstructionRecordType.ConstructionCrew });
        fixture.Db.AddRange(crew, linkedAndConstructed, constructionOnly);
        await fixture.Db.SaveChangesAsync();

        var summary = await fixture.Service.GetAsync(
            new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, crew.Id, Today()), CancellationToken.None);

        summary.Projects.TotalCount.Should().Be(2);
        summary.Projects.UnderConstructionCount.Should().Be(1);
        summary.Projects.SettledArchivedCount.Should().Be(1);
        summary.IsConstructionCrew.Should().BeTrue();
    }

    [Fact]
    public async Task PersonnelSummaryUsesCurrentPrimaryHistoriesAndCurrentCrewMembershipsOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        var company = new LegalEntity { Code = "LE-PEOPLE", Name = "人员公司", ShortName = "人员公司" };
        var crew = new BusinessPartner { PartnerNumber = "CREW-PEOPLE", Name = "人员班组", ShortName = "人员班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        fixture.Db.AddRange(company, crew);

        var formal = Person("RY-FORMAL", true);
        var temporary = Person("RY-TEMP", true);
        var inactive = Person("RY-INACTIVE", false);
        var ended = Person("RY-ENDED", true);
        fixture.Db.People.AddRange(formal, temporary, inactive, ended);
        fixture.Db.PersonnelEngagementHistories.AddRange(
            Internal(formal, company, EmployeeType.Formal, Today().AddDays(-20), null),
            Internal(temporary, company, EmployeeType.Temporary, Today().AddDays(-10), null),
            Internal(inactive, company, EmployeeType.Labor, Today().AddDays(-10), null),
            Internal(ended, company, EmployeeType.Labor, Today().AddDays(-20), Today().AddDays(-1)));

        var crewPerson = Person("RY-CREW", true);
        var crewWorker = new ConstructionWorker { Name = "当前班组人员", Person = crewPerson };
        var endedPerson = Person("RY-CREW-ENDED", true);
        var endedWorker = new ConstructionWorker { Name = "已退组人员", Person = endedPerson };
        fixture.Db.ConstructionWorkers.AddRange(crewWorker, endedWorker);
        fixture.Db.PersonnelEngagementHistories.Add(new PersonnelEngagementHistory
        {
            Person = crewPerson,
            Scope = PersonnelScope.External,
            ExternalType = ExternalPersonnelType.ConstructionCrew,
            BusinessPartner = crew,
            CrewBusinessPartner = crew,
            StartDate = Today().AddDays(-5),
            IsPrimary = true,
            Reason = "当前班组归属"
        });
        fixture.Db.ConstructionCrewMemberships.AddRange(
            new ConstructionCrewMembership { Worker = crewWorker, CrewBusinessPartner = crew, StartDate = Today().AddDays(-5), IsPrimary = true },
            new ConstructionCrewMembership { Worker = endedWorker, CrewBusinessPartner = crew, StartDate = Today().AddDays(-10), EndDate = Today().AddDays(-1), IsPrimary = true });
        await fixture.Db.SaveChangesAsync();

        var companySummary = await fixture.Service.GetAsync(
            new OrganizationSummaryQuery(OrganizationOwnerKind.LegalEntity, company.Id, Today()), CancellationToken.None);
        var crewSummary = await fixture.Service.GetAsync(
            new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, crew.Id, Today()), CancellationToken.None);

        companySummary.Personnel.TotalCurrentCount.Should().Be(3);
        companySummary.Personnel.ActiveCount.Should().Be(2);
        companySummary.Personnel.FormalCount.Should().Be(1);
        companySummary.Personnel.TemporaryCount.Should().Be(1);
        companySummary.Personnel.LaborCount.Should().Be(0);
        crewSummary.Personnel.TotalCurrentCount.Should().Be(1);
        crewSummary.Personnel.ActiveCount.Should().Be(1);
        crewSummary.Personnel.ConstructionCrewCount.Should().Be(1);
    }

    private static Person Person(string number, bool isActive) => new() { PersonNumber = number, Name = number, IsActive = isActive };

    private static PersonnelEngagementHistory Internal(Person person, LegalEntity company, EmployeeType type, DateOnly start, DateOnly? end) => new()
    {
        Person = person,
        Scope = PersonnelScope.Internal,
        InternalType = type,
        LegalEntity = company,
        StartDate = start,
        EndDate = end,
        IsPrimary = true,
        Reason = "测试归属"
    };

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public OrganizationSummaryService Service { get; } = new(db);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
