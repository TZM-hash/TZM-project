using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Equipment;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class EquipmentOwnershipMaintenanceTests
{
    [Fact]
    public async Task TransferKeepsHistoryWithoutFinanceAndMaintenanceIsOptional()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new LegalEntity { Code = "OWN-A", Name = "原公司", ShortName = "原" };
        var target = new LegalEntity { Code = "OWN-B", Name = "新公司", ShortName = "新" };
        var equipment = new Equipment { EquipmentNumber = "OWN-E", Name = "自有设备", OwnershipType = EquipmentOwnershipType.SelfOwned, OwnerLegalEntity = source };
        db.AddRange(source, target, equipment);
        await db.SaveChangesAsync();
        var service = new EquipmentService(db);

        await service.TransferOwnershipAsync(EquipmentActor.Administrator("admin"), new TransferEquipmentOwnershipRequest(equipment.Id, EquipmentTransferType.InternalCompany, new DateOnly(2026, 8, 1), target.Id, null, null, "内部调拨"), default);
        await service.SaveMaintenanceAsync(EquipmentActor.Administrator("admin"), new SaveEquipmentMaintenanceRequest(null, equipment.Id, null, null, new DateOnly(2026, 9, 1), null, null, null, "记录维保"), default);

        (await db.Equipment.SingleAsync()).OwnerLegalEntityId.Should().Be(target.Id);
        (await db.EquipmentOwnershipHistories.SingleAsync()).FromLegalEntityId.Should().Be(source.Id);
        (await db.EquipmentMaintenanceRecords.SingleAsync()).NextDueDate.Should().Be(new DateOnly(2026, 9, 1));
        (await db.PayableEntries.CountAsync()).Should().Be(0);
    }
}
