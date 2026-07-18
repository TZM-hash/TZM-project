using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Equipment;
using EngineeringManager.Infrastructure.Partners;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectConstructionServiceTests
{
    [Fact]
    public async Task SavesMultipleCyclesAndCreatesLinkedDraftInDestinationProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new Project { ProjectNumber = "CONS-01", Name = "来源项目", Stage = ProjectStage.UnderConstruction };
        var target = new Project { ProjectNumber = "CONS-02", Name = "目标项目", Stage = ProjectStage.UnderConstruction };
        var equipment = new Equipment { EquipmentNumber = "EQ-CONS-01", Name = "履带吊", OwnershipType = EquipmentOwnershipType.SelfOwned };
        db.AddRange(source, target, equipment);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");

        var first = await service.SaveAsync(actor, new SaveProjectConstructionRecordRequest(null, source.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, target.Id, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), 2, "首轮施工", false, null, "登记首轮施工"), new DateOnly(2026, 7, 17), CancellationToken.None);
        await service.SaveAsync(actor, new SaveProjectConstructionRecordRequest(null, source.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null, new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 15), 0, "第二轮施工", false, null, "登记第二轮施工"), new DateOnly(2026, 7, 17), CancellationToken.None);

        first.TotalDays.Should().Be(10);
        first.WorkDays.Should().Be(8);
        var sourceRows = await db.ProjectConstructionRecords.Where(item => item.ProjectId == source.Id).ToListAsync();
        sourceRows.Should().HaveCount(2);
        var draft = await db.ProjectConstructionRecords.SingleAsync(item => item.ProjectId == target.Id);
        draft.IsDraft.Should().BeTrue();
        draft.PreviousRecordId.Should().Be(first.Id);
        draft.TransferFromProjectId.Should().Be(source.Id);
        sourceRows.Single(item => item.Id == first.Id).NextRecordId.Should().Be(draft.Id);
        (await db.AuditLogs.CountAsync(item => item.Action == "CreateProjectConstruction")).Should().Be(2);
    }

    [Fact]
    public async Task RejectsSelfTransferAndStaleUpdate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { ProjectNumber = "CONS-03", Name = "并发项目", Stage = ProjectStage.UnderConstruction };
        var equipment = new Equipment { EquipmentNumber = "EQ-CONS-02", Name = "挖机", OwnershipType = EquipmentOwnershipType.SelfOwned };
        db.AddRange(project, equipment);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");

        var selfTransfer = () => service.SaveAsync(actor, new SaveProjectConstructionRecordRequest(null, project.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, project.Id, new DateOnly(2026, 7, 1), null, 0, null, false, null, "错误流转"), new DateOnly(2026, 7, 17), CancellationToken.None);
        await selfTransfer.Should().ThrowAsync<ArgumentException>().WithMessage("*不能是当前项目*");
        var saved = await service.SaveAsync(actor, new SaveProjectConstructionRecordRequest(null, project.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null, new DateOnly(2026, 7, 1), null, 0, null, false, null, "新增"), new DateOnly(2026, 7, 17), CancellationToken.None);
        var stale = () => service.SaveAsync(actor, new SaveProjectConstructionRecordRequest(saved.Id, project.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null, new DateOnly(2026, 7, 1), null, 0, null, false, Guid.NewGuid(), "过期修改"), new DateOnly(2026, 7, 17), CancellationToken.None);
        await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
