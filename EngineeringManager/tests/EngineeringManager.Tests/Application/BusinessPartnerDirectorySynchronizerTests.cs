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
        (await fixture.Db.BusinessPartnerRoles.CountAsync(item => item.RoleType == BusinessPartnerRoleType.MaterialSupplier)).Should().Be(2);
        (await fixture.Db.BusinessPartnerRoles.CountAsync(item => item.RoleType == BusinessPartnerRoleType.CustomerOrGeneralContractor)).Should().Be(0);
        (await fixture.Db.ProjectPartners.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SynchronizeClassifiesRolelessLegacyPartnersWithoutOverwritingExistingRoles()
    {
        await using var fixture = await DirectoryFixture.CreateAsync();
        var namedCrew = new BusinessPartner { PartnerNumber = "HZ-LEGACY-CREW", Name = "李高兴钢筋班组", ShortName = "钢筋班组" };
        var tradeCrew = new BusinessPartner { PartnerNumber = "HZ-LEGACY-TRADE", Name = "钢筋工", ShortName = "钢筋工" };
        var customer = new BusinessPartner { PartnerNumber = "HZ-LEGACY-CUSTOMER", Name = "项目甲方代表", ShortName = "甲方" };
        var supplier = new BusinessPartner { PartnerNumber = "HZ-LEGACY-SUPPLIER", Name = "普通材料商贸有限公司", ShortName = "普通材料" };
        var existing = new BusinessPartner { PartnerNumber = "HZ-LEGACY-EXISTING", Name = "既有零星供应商", ShortName = "既有零星" };
        existing.Roles.Add(new BusinessPartnerRole
        {
            Partner = existing,
            RoleType = BusinessPartnerRoleType.MiscellaneousSupplier
        });
        fixture.Db.BusinessPartners.AddRange(namedCrew, tradeCrew, customer, supplier, existing);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.SynchronizeAsync(null, CancellationToken.None);
        await fixture.Service.SynchronizeAsync(null, CancellationToken.None);

        (await fixture.Db.BusinessPartnerRoles.SingleAsync(item => item.BusinessPartnerId == namedCrew.Id)).RoleType
            .Should().Be(BusinessPartnerRoleType.ConstructionCrew);
        (await fixture.Db.BusinessPartnerRoles.SingleAsync(item => item.BusinessPartnerId == tradeCrew.Id)).RoleType
            .Should().Be(BusinessPartnerRoleType.ConstructionCrew);
        (await fixture.Db.BusinessPartnerRoles.SingleAsync(item => item.BusinessPartnerId == customer.Id)).RoleType
            .Should().Be(BusinessPartnerRoleType.CustomerOrGeneralContractor);
        (await fixture.Db.BusinessPartnerRoles.SingleAsync(item => item.BusinessPartnerId == supplier.Id)).RoleType
            .Should().Be(BusinessPartnerRoleType.MaterialSupplier);
        (await fixture.Db.BusinessPartnerRoles.SingleAsync(item => item.BusinessPartnerId == existing.Id)).RoleType
            .Should().Be(BusinessPartnerRoleType.MiscellaneousSupplier);
        (await fixture.Db.BusinessPartnerRoles.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task SynchronizeRemovesStaleAutomaticGeneralContractorLinkAfterProjectNameChangesOrClears()
    {
        await using var fixture = await DirectoryFixture.CreateAsync();
        var project = new Project
        {
            ProjectNumber = "XM-RENAME",
            Name = "总包改名项目",
            GeneralContractorName = "旧总包有限公司"
        };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

        project.GeneralContractorName = "新总包有限公司";
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

        var renamedLinks = await fixture.Db.ProjectPartners
            .Include(item => item.Partner)
            .Where(item => item.ProjectId == project.Id)
            .ToArrayAsync();
        renamedLinks.Should().ContainSingle();
        renamedLinks[0].Partner.Name.Should().Be("新总包有限公司");

        project.GeneralContractorName = null;
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

        (await fixture.Db.ProjectPartners.CountAsync(item => item.ProjectId == project.Id)).Should().Be(0);
    }

    [Fact]
    public async Task SynchronizeKeepsContractBoundStaleGeneralContractorLinkAsInactiveHistory()
    {
        await using var fixture = await DirectoryFixture.CreateAsync();
        var project = new Project
        {
            ProjectNumber = "XM-CONTRACT-LINK",
            Name = "总包合同关联项目",
            GeneralContractorName = "合同总包有限公司"
        };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

        var partner = await fixture.Db.BusinessPartners.SingleAsync(item => item.Name == "合同总包有限公司");
        var contract = new Contract
        {
            ProjectId = project.Id,
            ContractNumber = "CONTRACT-LINK-001",
            Name = "总包合同",
            ContractType = ContractType.MainContract
        };
        var link = await fixture.Db.ProjectPartners.SingleAsync(item => item.ProjectId == project.Id && item.BusinessPartnerId == partner.Id);
        link.Contract = contract;
        fixture.Db.Contracts.Add(contract);
        await fixture.Db.SaveChangesAsync();

        project.GeneralContractorName = null;
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

        var preserved = await fixture.Db.ProjectPartners.SingleAsync(item => item.Id == link.Id);
        preserved.IsActive.Should().BeFalse();
        preserved.ContractId.Should().Be(contract.Id);
        (await fixture.Db.Contracts.CountAsync(item => item.Id == contract.Id)).Should().Be(1);

        project.GeneralContractorName = "合同总包有限公司";
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.SynchronizeAsync(project.Id, CancellationToken.None);

        preserved = await fixture.Db.ProjectPartners.SingleAsync(item => item.Id == link.Id);
        preserved.IsActive.Should().BeTrue();
        preserved.ContractId.Should().Be(contract.Id);
        (await fixture.Db.ProjectPartners.CountAsync(item => item.ProjectId == project.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentSynchronizationsCreateOnePartnerWithoutUniqueKeyFailures()
    {
        var databaseName = $"partner-directory-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=5";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        Guid projectId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var project = new Project
            {
                ProjectNumber = "XM-CONCURRENT",
                Name = "并发同步项目",
                GeneralContractorName = "并发总包有限公司"
            };
            setup.Projects.Add(project);
            await setup.SaveChangesAsync();
            projectId = project.Id;
        }

        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var synchronizations = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = new ApplicationDbContext(options);
            await start.Task;
            await new BusinessPartnerDirectorySynchronizer(db).SynchronizeAsync(projectId, CancellationToken.None);
        }).ToArray();

        start.SetResult(true);
        var action = async () => await Task.WhenAll(synchronizations);

        await action.Should().NotThrowAsync();
        await using var verification = new ApplicationDbContext(options);
        (await verification.BusinessPartners.CountAsync()).Should().Be(1);
        (await verification.BusinessPartnerRoles.CountAsync()).Should().Be(1);
        (await verification.ProjectPartners.CountAsync()).Should().Be(1);
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
