using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.EquipmentOffline;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Equipment;
using EngineeringManager.Infrastructure.EquipmentOffline;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class EquipmentOfflineServiceTests
{
    [Fact]
    public async Task OfflineUsageSyncIsIdempotentAndServerRevalidatesPeriods()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var user = new ApplicationUser { UserName = "offline-equipment", NormalizedUserName = "OFFLINE-EQUIPMENT", DisplayName = "设备离线用户" };
        var company = new LegalEntity { Code = "OFF-C", Name = "离线公司", ShortName = "离线" };
        var project = new Project { ProjectNumber = "OFF-P", Name = "离线项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var equipment = new Equipment { EquipmentNumber = "OFF-E", Name = "离线设备", OwnershipType = EquipmentOwnershipType.SelfOwned, OwnerLegalEntity = company };
        db.AddRange(user, company, project, equipment);
        await db.SaveChangesAsync();
        var service = new EquipmentOfflineService(db, new EquipmentService(db));
        var actor = EquipmentActor.Administrator(user.Id);
        var clientDraftId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var usage = new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 2), RentMode.Daily, MonthlyProrationMode.ThirtyDay, 0m, false, null, [new EquipmentPeriodRequest(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 2), EquipmentPeriodType.Work, true, "离线施工")], null, "设备离线同步");

        var first = await service.SyncAsync(actor, new EquipmentOfflineSyncRequest(clientDraftId, operationId, null, usage), default);
        var second = await service.SyncAsync(actor, new EquipmentOfflineSyncRequest(clientDraftId, operationId, first.ServerVersion, usage), default);

        first.IsIdempotent.Should().BeFalse();
        second.IsIdempotent.Should().BeTrue();
        second.UsageId.Should().Be(first.UsageId);
        (await db.EquipmentProjectUsages.CountAsync()).Should().Be(1);
    }
}
