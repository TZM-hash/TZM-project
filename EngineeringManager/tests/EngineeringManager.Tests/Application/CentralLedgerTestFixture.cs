using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

internal sealed class CentralLedgerTestFixture : IAsyncDisposable
{
    private readonly HashSet<Guid> projectIds = [];

    private CentralLedgerTestFixture(SqliteConnection connection, ApplicationDbContext db)
    {
        Connection = connection;
        Db = db;
    }

    public SqliteConnection Connection { get; }
    public ApplicationDbContext Db { get; }
    public LegalEntity LegalEntity { get; private set; } = null!;
    public LegalEntity CounterLegalEntity { get; private set; } = null!;
    public Project Project { get; private set; } = null!;
    public Contract Contract { get; private set; } = null!;
    public ContractLineItem LineItem { get; private set; } = null!;
    public BusinessPartner Client { get; private set; } = null!;
    public BusinessPartner Supplier { get; private set; } = null!;
    public BusinessPartner Crew { get; private set; } = null!;
    public FinancialAccount CollectionAccount { get; private set; } = null!;
    public FinancialAccount PaymentAccount { get; private set; } = null!;

    public static async Task<CentralLedgerTestFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            await command.ExecuteNonQueryAsync();
        }

        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var fixture = new CentralLedgerTestFixture(connection, db);
        await fixture.SeedAsync();
        return fixture;
    }

    public CentralLedgerActor ExternalActor(bool canManage = true)
    {
        return new CentralLedgerActor(
            "external-user",
            "外部账用户",
            new HashSet<Guid> { LegalEntity.Id },
            new HashSet<Guid>(projectIds),
            canManage,
            false,
            false,
            false);
    }

    public CentralLedgerActor InternalActor(bool canManage = true)
    {
        return new CentralLedgerActor(
            "internal-user",
            "内部账用户",
            new HashSet<Guid> { LegalEntity.Id, CounterLegalEntity.Id },
            new HashSet<Guid>(),
            false,
            canManage,
            false,
            false);
    }

    public CentralLedgerActor ReadOnlyActor()
    {
        return new CentralLedgerActor(
            "readonly-user",
            "只读用户",
            new HashSet<Guid> { LegalEntity.Id },
            new HashSet<Guid>(projectIds),
            false,
            false,
            false,
            false);
    }

    public void GrantProjectAccess(Guid projectId) => projectIds.Add(projectId);

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await Connection.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        LegalEntity = new LegalEntity { Code = "CENTRAL-A", Name = "中央账本甲公司", ShortName = "甲公司" };
        CounterLegalEntity = new LegalEntity { Code = "CENTRAL-B", Name = "中央账本乙公司", ShortName = "乙公司" };
        Client = new BusinessPartner { PartnerNumber = "CLIENT", Name = "客户单位", ShortName = "客户" };
        Supplier = new BusinessPartner { PartnerNumber = "SUPPLIER", Name = "合作商单位", ShortName = "合作商" };
        Crew = new BusinessPartner { PartnerNumber = "CREW", Name = "施工班组", ShortName = "班组" };
        Crew.Roles.Add(new BusinessPartnerRole { Partner = Crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        Project = new Project { ProjectNumber = "CENTRAL-P", Name = "中央账本项目", Stage = ProjectStage.UnderConstruction };
        Contract = new Contract { Project = Project, ContractNumber = "CENTRAL-C", Name = "中央账本合同", BusinessPartner = Client, TotalAmount = 1_000_000m };
        LineItem = new ContractLineItem
        {
            Contract = Contract,
            Code = "001",
            Name = "工程量",
            Unit = "项",
            EstimatedQuantity = 1m,
            EstimatedUnitPrice = 1_000_000m
        };
        Contract.LineItems.Add(LineItem);
        Project.Contracts.Add(Contract);
        Project.LegalEntities.Add(new ProjectLegalEntity { Project = Project, LegalEntity = LegalEntity, IsPrimary = true });
        projectIds.Add(Project.Id);
        CollectionAccount = new FinancialAccount { LegalEntity = LegalEntity, AccountName = "收款账户", AccountType = FinancialAccountType.Bank };
        PaymentAccount = new FinancialAccount { LegalEntity = LegalEntity, AccountName = "付款账户", AccountType = FinancialAccountType.Bank };

        Db.AddRange(LegalEntity, CounterLegalEntity, Client, Supplier, Crew, Project, CollectionAccount, PaymentAccount);
        await Db.SaveChangesAsync();
    }
}
