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

    [Fact]
    public async Task PartnerSummaryExcludesInternalPersonnelAssignedToTheCrew()
    {
        await using var fixture = await Fixture.CreateAsync();
        var company = new LegalEntity { Code = "LE-CREW-ASSIGN", Name = "派驻公司", ShortName = "派驻公司" };
        var crew = new BusinessPartner { PartnerNumber = "CREW-INTERNAL", Name = "内部派驻班组", ShortName = "内部派驻班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var employee = Person("RY-INTERNAL-CREW", true);
        fixture.Db.AddRange(company, crew, employee);
        fixture.Db.PersonnelEngagementHistories.Add(new PersonnelEngagementHistory
        {
            Person = employee,
            Scope = PersonnelScope.Internal,
            InternalType = EmployeeType.Formal,
            LegalEntity = company,
            CrewBusinessPartner = crew,
            StartDate = Today().AddDays(-10),
            IsPrimary = true,
            Reason = "内部人员派驻班组"
        });
        await fixture.Db.SaveChangesAsync();

        var direct = await fixture.Service.GetAsync(
            new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, crew.Id, Today()), CancellationToken.None);
        var batch = (await fixture.Service.GetManyAsync(
            [new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, crew.Id, Today())],
            CancellationToken.None)).Single();

        direct.Personnel.TotalCurrentCount.Should().Be(0);
        batch.Personnel.TotalCurrentCount.Should().Be(0);
        direct.Personnel.FormalCount.Should().Be(0);
        batch.Personnel.FormalCount.Should().Be(0);
    }

    [Fact]
    public async Task PartnerSummaryExcludesMembershipOnlyWorkersThatAreAbsentFromPersonnelFilter()
    {
        await using var fixture = await Fixture.CreateAsync();
        var crew = new BusinessPartner { PartnerNumber = "CREW-LEGACY", Name = "历史名册班组", ShortName = "历史班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var worker = new ConstructionWorker { Name = "仅名册人员", IsActive = true };
        worker.Memberships.Add(new ConstructionCrewMembership
        {
            Worker = worker,
            CrewBusinessPartner = crew,
            StartDate = Today().AddDays(-10),
            IsPrimary = true
        });
        fixture.Db.AddRange(crew, worker);
        await fixture.Db.SaveChangesAsync();

        var direct = await fixture.Service.GetAsync(
            new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, crew.Id, Today()), CancellationToken.None);
        var batch = (await fixture.Service.GetManyAsync(
            [new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, crew.Id, Today())],
            CancellationToken.None)).Single();

        direct.Personnel.TotalCurrentCount.Should().Be(0);
        batch.Personnel.TotalCurrentCount.Should().Be(0);
    }

    [Fact]
    public async Task BatchSummaryLoadsSeveralPartnerOrganizationsInOneRequest()
    {
        await using var fixture = await Fixture.CreateAsync();
        var crew = new BusinessPartner { PartnerNumber = "CREW-BATCH", Name = "批量班组", ShortName = "批量班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var supplier = new BusinessPartner { PartnerNumber = "SUP-BATCH", Name = "批量供应商", ShortName = "批量供应商" };
        supplier.Roles.Add(new BusinessPartnerRole { Partner = supplier, RoleType = BusinessPartnerRoleType.MaterialSupplier });
        var crewProject = new Project { ProjectNumber = "P-CREW-BATCH", Name = "班组批量项目", Stage = ProjectStage.UnderConstruction };
        crewProject.Partners.Add(new ProjectPartner { Project = crewProject, Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var supplierProject = new Project { ProjectNumber = "P-SUP-BATCH", Name = "供应商批量项目", Stage = ProjectStage.SettledArchived };
        supplierProject.Partners.Add(new ProjectPartner { Project = supplierProject, Partner = supplier, RoleType = BusinessPartnerRoleType.MaterialSupplier });
        fixture.Db.AddRange(crew, supplier, crewProject, supplierProject);
        fixture.Db.OrganizationUnits.AddRange(
            new OrganizationUnit { Code = "CREW-DEPT", Name = "班组部门", UnitType = OrganizationUnitType.Department, BusinessPartnerId = crew.Id, IsActive = true },
            new OrganizationUnit { Code = "SUP-DEPT", Name = "供应商部门", UnitType = OrganizationUnitType.Department, BusinessPartnerId = supplier.Id, IsActive = false });
        var supplierPerson = Person("RY-SUP-BATCH", true);
        fixture.Db.People.Add(supplierPerson);
        fixture.Db.PersonnelEngagementHistories.Add(new PersonnelEngagementHistory
        {
            Person = supplierPerson,
            Scope = PersonnelScope.External,
            ExternalType = ExternalPersonnelType.BusinessPartner,
            BusinessPartner = supplier,
            StartDate = Today().AddDays(-5),
            IsPrimary = true,
            Reason = "批量汇总测试"
        });
        await fixture.Db.SaveChangesAsync();

        var summaries = await fixture.Service.GetManyAsync(
            [
                new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, crew.Id, Today()),
                new OrganizationSummaryQuery(OrganizationOwnerKind.BusinessPartner, supplier.Id, Today())
            ],
            CancellationToken.None);

        summaries.Should().HaveCount(2);
        var byId = summaries.ToDictionary(item => item.Query.Id);
        byId[crew.Id].IsConstructionCrew.Should().BeTrue();
        byId[crew.Id].Projects.UnderConstructionCount.Should().Be(1);
        byId[crew.Id].Departments.ActiveCount.Should().Be(1);
        byId[supplier.Id].IsConstructionCrew.Should().BeFalse();
        byId[supplier.Id].Projects.SettledArchivedCount.Should().Be(1);
        byId[supplier.Id].Personnel.BusinessPartnerCount.Should().Be(1);
        byId[supplier.Id].Departments.ActiveCount.Should().Be(0);
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
