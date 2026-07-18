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
using System.Text.Json;

namespace EngineeringManager.Tests.Application;

public sealed class EquipmentServiceTests
{
    [Fact]
    public async Task EquipmentNotesRoundTripAndEnterAuditLog()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-N", Name = "设备备注公司", ShortName = "设备备注" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();

        var saved = await scope.Service.SaveEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            new SaveEquipmentRequest(null, "EQ-NOTES", "备注设备", null, null, EquipmentOwnershipType.SelfOwned, company.Id, null, null, null, "新增", "设备备注"),
            default);

        saved.Notes.Should().Be("设备备注");
        (await scope.Db.Equipment.SingleAsync(item => item.Id == saved.Id)).Notes.Should().Be("设备备注");
        var audit = await scope.Db.AuditLogs.SingleAsync(item => item.EntityType == nameof(Equipment));
        using var after = JsonDocument.Parse(audit.AfterJson!);
        after.RootElement.GetProperty("Notes").GetString().Should().Be("设备备注");
    }

    [Fact]
    public async Task EquipmentCanBeSavedCopiedAndAssignedWithoutOverlappingUsage()
    {
        await using var scope = await CreateScopeAsync();
        var actor = EquipmentActor.Administrator("admin");
        var company = new LegalEntity { Code = "EQ-C", Name = "设备公司", ShortName = "设备" };
        var project = new Project { ProjectNumber = "EQ-P1", Name = "设备项目一", Stage = ProjectStage.UnderConstruction };
        var projectTwo = new Project { ProjectNumber = "EQ-P2", Name = "设备项目二", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        projectTwo.LegalEntities.Add(new ProjectLegalEntity { Project = projectTwo, LegalEntity = company, IsPrimary = true });
        scope.Db.AddRange(company, project, projectTwo);
        await scope.Db.SaveChangesAsync();

        var equipment = await scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "EQ-001", "测试挖机", "X1", "挖掘机", EquipmentOwnershipType.SelfOwned, company.Id, null, 500m, null, "新增"), default);
        var copy = await scope.Service.CopyEquipmentAsync(actor, equipment.Id, default);
        var usage = await scope.Service.SaveUsageAsync(actor, new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), RentMode.Daily, MonthlyProrationMode.ThirtyDay, 500m, false, null, [new EquipmentPeriodRequest(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), EquipmentPeriodType.Work, true, null)], null, "进场"), default);

        copy.EquipmentNumber.Should().BeEmpty();
        copy.Name.Should().Contain("副本");
        usage.TotalDays.Should().Be(10);
        (await scope.Db.Equipment.SingleAsync(item => item.Id == equipment.Id)).Status.Should().Be(EquipmentStatus.Idle);
        var overlap = () => scope.Service.SaveUsageAsync(actor, new SaveEquipmentUsageRequest(null, equipment.Id, projectTwo.Id, company.Id, null, new DateOnly(2026, 7, 5), null, RentMode.Daily, MonthlyProrationMode.ThirtyDay, 500m, false, null, [], null, "重叠"), default);
        await overlap.Should().ThrowAsync<InvalidOperationException>().WithMessage("*重叠*");
    }

    [Fact]
    public async Task RentedEquipmentRequiresLessorAndSelfOwnedEquipmentRequiresCompany()
    {
        await using var scope = await CreateScopeAsync();
        var actor = EquipmentActor.Administrator("admin");
        var selfOwned = () => scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "A", "自有", null, null, EquipmentOwnershipType.SelfOwned, null, null, null, null, "测试"), default);
        var rented = () => scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "B", "租赁", null, null, EquipmentOwnershipType.Rented, null, null, null, null, "测试"), default);
        await selfOwned.Should().ThrowAsync<ArgumentException>().WithMessage("*所属公司*");
        await rented.Should().ThrowAsync<ArgumentException>().WithMessage("*出租方*");
    }

    private static async Task<TestScope> CreateScopeAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return new TestScope(connection, db, new EquipmentService(db));
    }

    private sealed class TestScope(SqliteConnection connection, ApplicationDbContext db, EquipmentService service) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public EquipmentService Service { get; } = service;
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
