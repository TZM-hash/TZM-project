using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Partners;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class BusinessPartnerDirectorySynchronizerTests
{
    [Fact]
    public async Task SynchronizeCreatesAndClassifiesProjectDerivedPartnersIdempotently()
    {
        await using var fixture = await DirectoryFixture.CreateAsync();
        var importedCrew = new BusinessPartner
        {
            PartnerNumber = "HZ0090",
            Name = "已有班组",
            ShortName = "已有班组"
        };
        var project = new Project
        {
            ProjectNumber = "XM-SYNC",
            Name = "同步项目",
            GeneralContractorName = ProjectGeneralContractors.Serialize(["甲方一有限公司", "总包二有限公司"])
        };
        project.Partners.Add(new ProjectPartner
        {
            Project = project,
            Partner = importedCrew,
            RoleType = BusinessPartnerRoleType.ConstructionCrew
        });
        fixture.Db.AddRange(importedCrew, project);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);
        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

        (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(3);
        (await fixture.Db.BusinessPartnerRoles.CountAsync(item => item.RoleType == BusinessPartnerRoleType.ConstructionCrew)).Should().Be(1);
        (await fixture.Db.BusinessPartnerRoles.CountAsync(item => item.RoleType == BusinessPartnerRoleType.CustomerOrGeneralContractor)).Should().Be(2);
        (await fixture.Db.ProjectPartners.CountAsync()).Should().Be(3);
        (await fixture.Db.BusinessPartners.Select(item => item.PartnerNumber).Distinct().CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task SynchronizeRepairsConstructionRecordCrewRole()
    {
        await using var fixture = await DirectoryFixture.CreateAsync();
        var crew = new BusinessPartner { PartnerNumber = "HZ-CREW", Name = "记录班组", ShortName = "记录班组" };
        var project = new Project { ProjectNumber = "XM-CREW", Name = "班组项目" };
        project.ConstructionRecords.Add(new ProjectConstructionRecord
        {
            Project = project,
            RecordType = ProjectConstructionRecordType.ConstructionCrew,
            CrewBusinessPartner = crew
        });
        fixture.Db.AddRange(crew, project);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

        (await fixture.Db.BusinessPartnerRoles.SingleAsync()).RoleType.Should().Be(BusinessPartnerRoleType.ConstructionCrew);
    }

    [Fact]
    public async Task SynchronizeSkipsAmbiguousNameMatches()
    {
        await using var fixture = await DirectoryFixture.CreateAsync();
        fixture.Db.BusinessPartners.AddRange(
            new BusinessPartner { PartnerNumber = "HZ-A", Name = "冲突单位甲", ShortName = "冲突单位" },
            new BusinessPartner { PartnerNumber = "HZ-B", Name = "冲突单位乙", ShortName = "冲突单位" });
        fixture.Db.Projects.Add(new Project
        {
            ProjectNumber = "XM-AMB",
            Name = "歧义项目",
            GeneralContractorName = "冲突单位"
        });
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.SynchronizeAsync(null, CancellationToken.None);

        (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(2);
        (await fixture.Db.BusinessPartnerRoles.CountAsync()).Should().Be(0);
        (await fixture.Db.ProjectPartners.CountAsync()).Should().Be(0);
    }

    private sealed class DirectoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private DirectoryFixture(SqliteConnection connection, ApplicationDbContext db)
        {
            this.connection = connection;
            Db = db;
            Service = new BusinessPartnerDirectorySynchronizer(db);
        }

        public ApplicationDbContext Db { get; }
        public BusinessPartnerDirectorySynchronizer Service { get; }

        public static async Task<DirectoryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new DirectoryFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
