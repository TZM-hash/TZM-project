using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Equipment;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class EquipmentSettlementServiceTests
{
    [Fact]
    public async Task FinalSettlementCalculatesAdjustmentsOffsetsAndCreatesOnlyOnePayable()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "S-C", Name = "结算公司", ShortName = "结算" };
        var partner = new BusinessPartner { PartnerNumber = "S-B", Name = "出租方", ShortName = "出租" };
        partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = BusinessPartnerRoleType.MiscellaneousSupplier });
        var project = new Project { ProjectNumber = "S-P", Name = "结算项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var equipment = new Equipment { EquipmentNumber = "S-E", Name = "租赁设备", OwnershipType = EquipmentOwnershipType.Rented, LessorBusinessPartner = partner };
        var usage = new EquipmentProjectUsage { Equipment = equipment, Project = project, LegalEntity = company, EntryDate = new DateOnly(2026, 7, 1), ExitDate = new DateOnly(2026, 7, 10), RentMode = RentMode.Daily, UnitRate = 100m };
        usage.Periods.Add(new EquipmentWorkPeriod { Usage = usage, StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 10), PeriodType = EquipmentPeriodType.Work, IsChargeable = true });
        usage.AdvancePayments.Add(new EquipmentAdvancePayment { Usage = usage, PaymentType = EquipmentAdvancePaymentType.Prepayment, PaymentDate = new DateOnly(2026, 7, 1), Amount = 200m });
        scope.Db.AddRange(company, partner, project, equipment, usage);
        await scope.Db.SaveChangesAsync();

        var result = await scope.Service.FinalizeAsync(EquipmentActor.Administrator("admin"), new FinalizeEquipmentSettlementRequest(usage.Id, new DateOnly(2026, 7, 10), [new EquipmentSettlementAdjustmentRequest(EquipmentAdjustmentDirection.Addition, "进退场费", 50m, "测试")], true, "首次结算", null), default);
        var secondId = await scope.Service.GeneratePayableAsync(EquipmentActor.Administrator("admin"), result.Id, default);

        result.BaseAmount.Should().Be(1000m);
        result.TotalAmount.Should().Be(1050m);
        result.OffsetAmount.Should().Be(200m);
        result.PayableAmount.Should().Be(850m);
        result.PayableEntryId.Should().NotBeNull();
        secondId.Should().Be(result.PayableEntryId!.Value);
        (await scope.Db.PayableEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SettlementRejectsUsageWithoutExitOrWithUnclassifiedDays()
    {
        await using var scope = await CreateScopeAsync();
        var usage = await SeedUsageAsync(scope.Db, null, false);
        var action = () => scope.Service.FinalizeAsync(EquipmentActor.Administrator("admin"), new FinalizeEquipmentSettlementRequest(usage.Id, new DateOnly(2026, 7, 10), [], false, "结算", null), default);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    private static async Task<EquipmentProjectUsage> SeedUsageAsync(ApplicationDbContext db, DateOnly? exitDate, bool addPeriods)
    {
        var company = new LegalEntity { Code = Guid.NewGuid().ToString("N"), Name = "公司", ShortName = "公司" };
        var partner = new BusinessPartner { PartnerNumber = Guid.NewGuid().ToString("N"), Name = "出租方", ShortName = "出租" };
        var project = new Project { ProjectNumber = Guid.NewGuid().ToString("N"), Name = "项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var equipment = new Equipment { EquipmentNumber = Guid.NewGuid().ToString("N"), Name = "设备", OwnershipType = EquipmentOwnershipType.Rented, LessorBusinessPartner = partner };
        var usage = new EquipmentProjectUsage { Equipment = equipment, Project = project, LegalEntity = company, EntryDate = new DateOnly(2026, 7, 1), ExitDate = exitDate, RentMode = RentMode.Daily, UnitRate = 100m };
        if (addPeriods && exitDate.HasValue) usage.Periods.Add(new EquipmentWorkPeriod { Usage = usage, StartDate = usage.EntryDate, EndDate = exitDate.Value, PeriodType = EquipmentPeriodType.Work, IsChargeable = true });
        db.AddRange(company, partner, project, equipment, usage);
        await db.SaveChangesAsync();
        return usage;
    }

    private static async Task<TestScope> CreateScopeAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return new TestScope(connection, db, new EquipmentSettlementService(db));
    }

    private sealed class TestScope(SqliteConnection connection, ApplicationDbContext db, EquipmentSettlementService service) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public EquipmentSettlementService Service { get; } = service;
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
