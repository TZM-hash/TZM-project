using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Partners;
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

        var first = await service.SaveAsync(actor, new SaveProjectConstructionRecordRequest(null, source.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), 2, "首轮施工", false, null, "登记首轮施工"), new DateOnly(2026, 7, 17), CancellationToken.None);
        await service.LinkNextAsync(actor, new LinkProjectConstructionRecordRequest(first.Id, target.Id, first.ConcurrencyStamp, "流转到目标项目", new DateOnly(2026, 7, 11)), new DateOnly(2026, 7, 17), CancellationToken.None);
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
        await selfTransfer.Should().ThrowAsync<InvalidOperationException>().WithMessage("*跨项目流转只能通过*");
        var saved = await service.SaveAsync(actor, new SaveProjectConstructionRecordRequest(null, project.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null, new DateOnly(2026, 7, 1), null, 0, null, false, null, "新增"), new DateOnly(2026, 7, 17), CancellationToken.None);
        var stale = () => service.SaveAsync(actor, new SaveProjectConstructionRecordRequest(saved.Id, project.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null, new DateOnly(2026, 7, 1), null, 0, null, false, Guid.NewGuid(), "过期修改"), new DateOnly(2026, 7, 17), CancellationToken.None);
        await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task OverviewFlagIsAllowedForEquipmentOnlyAndIsNotCopiedToTransferDraft()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new Project { ProjectNumber = "CONS-FLAG-01", Name = "机械标记来源项目" };
        var target = new Project { ProjectNumber = "CONS-FLAG-02", Name = "机械标记目标项目" };
        var equipment = new Equipment { EquipmentNumber = "EQ-CONS-FLAG", Name = "重要机械" };
        var crew = new BusinessPartner { PartnerNumber = "CREW-CONS-FLAG", Name = "施工班组", ShortName = "施工班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        db.AddRange(source, target, equipment, crew);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");

        var saved = await service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, source.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null,
                new DateOnly(2026, 7, 1), null, 0, null, false, null, "显示重要机械", ShowInProjectOverview: true),
            new DateOnly(2026, 7, 18), CancellationToken.None);

        await service.LinkNextAsync(actor, new LinkProjectConstructionRecordRequest(saved.Id, target.Id, saved.ConcurrencyStamp, "流转重要机械", new DateOnly(2026, 7, 19)), new DateOnly(2026, 7, 18), CancellationToken.None);

        saved.ShowInProjectOverview.Should().BeTrue();
        (await db.ProjectConstructionRecords.SingleAsync(item => item.Id == saved.Id)).ShowInProjectOverview.Should().BeTrue();
        (await db.ProjectConstructionRecords.SingleAsync(item => item.ProjectId == target.Id)).ShowInProjectOverview.Should().BeFalse();

        var crewAction = () => service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, source.Id, ProjectConstructionRecordType.ConstructionCrew, null, crew.Id, null, null,
                new DateOnly(2026, 7, 1), null, 0, null, false, null, "错误显示班组", ShowInProjectOverview: true),
            new DateOnly(2026, 7, 18), CancellationToken.None);
        await crewAction.Should().ThrowAsync<ArgumentException>().WithMessage("*施工班组不能显示在项目总览*");
    }

    [Fact]
    public async Task ExistingRecordCannotSwitchEquipmentSubject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var project = new Project { ProjectNumber = "CONS-LOCK-01", Name = "主体锁定项目" };
        var originalEquipment = new Equipment { EquipmentNumber = "EQ-LOCK-01", Name = "原设备" };
        var replacementEquipment = new Equipment { EquipmentNumber = "EQ-LOCK-02", Name = "替换设备" };
        db.AddRange(project, originalEquipment, replacementEquipment);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");
        var saved = await service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, project.Id, ProjectConstructionRecordType.Equipment, originalEquipment.Id, null, null, null,
                new DateOnly(2026, 7, 1), null, 0, null, false, null, "新增正式记录"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        var action = () => service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(saved.Id, project.Id, ProjectConstructionRecordType.Equipment, replacementEquipment.Id, null, null, null,
                new DateOnly(2026, 7, 1), null, 0, null, false, saved.ConcurrencyStamp, "尝试切换设备"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*不能直接切换设备或班组*");
        (await db.ProjectConstructionRecords.SingleAsync(item => item.Id == saved.Id)).EquipmentId.Should().Be(originalEquipment.Id);
    }

    [Fact]
    public async Task LinkNextCreatesBidirectionalDraftInTargetProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new Project { ProjectNumber = "CONS-LINK-01", Name = "连接来源项目" };
        var target = new Project { ProjectNumber = "CONS-LINK-02", Name = "连接目标项目" };
        var equipment = new Equipment { EquipmentNumber = "EQ-LINK-01", Name = "连接设备" };
        db.AddRange(source, target, equipment);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");
        var current = await service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, source.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null,
                new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20), 0, null, false, null, "新增施工记录"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        await service.LinkNextAsync(actor,
            new LinkProjectConstructionRecordRequest(current.Id, target.Id, current.ConcurrencyStamp, "关联后续项目", new DateOnly(2026, 7, 21)),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        var sourceRecord = await db.ProjectConstructionRecords.SingleAsync(item => item.Id == current.Id);
        var targetDraft = await db.ProjectConstructionRecords.SingleAsync(item => item.ProjectId == target.Id);
        sourceRecord.NextRecordId.Should().Be(targetDraft.Id);
        sourceRecord.TransferToProjectId.Should().Be(target.Id);
        targetDraft.PreviousRecordId.Should().Be(sourceRecord.Id);
        targetDraft.TransferFromProjectId.Should().Be(source.Id);
        targetDraft.IsDraft.Should().BeTrue();
    }

    [Fact]
    public async Task UnlinkClearsBothSidesOfConstructionFlow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new Project { ProjectNumber = "CONS-UNLINK-01", Name = "解除来源项目" };
        var target = new Project { ProjectNumber = "CONS-UNLINK-02", Name = "解除目标项目" };
        var equipment = new Equipment { EquipmentNumber = "EQ-UNLINK-01", Name = "解除设备" };
        db.AddRange(source, target, equipment);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");
        var current = await service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, source.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null,
                new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20), 0, null, false, null, "新增带流转记录"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        current = await service.LinkNextAsync(actor,
            new LinkProjectConstructionRecordRequest(current.Id, target.Id, current.ConcurrencyStamp, "关联后续项目", new DateOnly(2026, 7, 21)),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        await service.UnlinkAsync(actor,
            new UnlinkProjectConstructionRecordRequest(current.Id, current.ConcurrencyStamp, "解除项目流转"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        var sourceRecord = await db.ProjectConstructionRecords.SingleAsync(item => item.Id == current.Id);
        var targetDraft = await db.ProjectConstructionRecords.SingleAsync(item => item.ProjectId == target.Id);
        sourceRecord.NextRecordId.Should().BeNull();
        sourceRecord.TransferToProjectId.Should().BeNull();
        targetDraft.PreviousRecordId.Should().BeNull();
        targetDraft.TransferFromProjectId.Should().BeNull();
    }

    [Fact]
    public async Task LinkPreviousConnectsTheLatestFormalMatchingRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var previousProject = new Project { ProjectNumber = "CONS-PREV-01", Name = "上一项目" };
        var currentProject = new Project { ProjectNumber = "CONS-PREV-02", Name = "当前项目" };
        var equipment = new Equipment { EquipmentNumber = "EQ-PREV-01", Name = "反向连接设备" };
        db.AddRange(previousProject, currentProject, equipment);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");
        var previous = await service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, previousProject.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null,
                new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 0, null, false, null, "上一项目退场"),
            new DateOnly(2026, 7, 21), CancellationToken.None);
        var current = await service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, currentProject.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null,
                new DateOnly(2026, 7, 1), null, 0, null, false, null, "当前项目进场"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        await service.LinkPreviousAsync(actor,
            new LinkProjectConstructionRecordRequest(current.Id, previousProject.Id, current.ConcurrencyStamp, "连接上一项目"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        var savedPrevious = await db.ProjectConstructionRecords.SingleAsync(item => item.Id == previous.Id);
        var savedCurrent = await db.ProjectConstructionRecords.SingleAsync(item => item.Id == current.Id);
        savedPrevious.NextRecordId.Should().Be(current.Id);
        savedPrevious.TransferToProjectId.Should().Be(currentProject.Id);
        savedCurrent.PreviousRecordId.Should().Be(previous.Id);
        savedCurrent.TransferFromProjectId.Should().Be(previousProject.Id);
    }

    [Fact]
    public async Task LinkNextReusesOnlyAnUnlinkedDraftAndSetsTargetEntryDate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var source = new Project { ProjectNumber = "CONS-REUSE-01", Name = "复用来源项目" };
        var target = new Project { ProjectNumber = "CONS-REUSE-02", Name = "复用目标项目" };
        var equipment = new Equipment { EquipmentNumber = "EQ-REUSE-01", Name = "复用设备" };
        var draft = new ProjectConstructionRecord { Project = target, RecordType = ProjectConstructionRecordType.Equipment, Equipment = equipment, IsDraft = true };
        db.AddRange(source, target, equipment, draft);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");
        var current = await service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, source.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null,
                new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20), 0, null, false, null, "新增来源记录"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        await service.LinkNextAsync(actor,
            new LinkProjectConstructionRecordRequest(current.Id, target.Id, current.ConcurrencyStamp, "复用目标草稿", new DateOnly(2026, 7, 22)),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        (await db.ProjectConstructionRecords.CountAsync(item => item.ProjectId == target.Id)).Should().Be(1);
        var reused = await db.ProjectConstructionRecords.SingleAsync(item => item.Id == draft.Id);
        reused.IsDraft.Should().BeTrue();
        reused.EntryDate.Should().Be(new DateOnly(2026, 7, 22));
        reused.PreviousRecordId.Should().Be(current.Id);
        reused.TransferFromProjectId.Should().Be(source.Id);
    }

    [Fact]
    public async Task LinkNextRequiresEntryDateAndRejectsAFlowCycle()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var firstProject = new Project { ProjectNumber = "CONS-CYCLE-01", Name = "循环项目一" };
        var secondProject = new Project { ProjectNumber = "CONS-CYCLE-02", Name = "循环项目二" };
        var equipment = new Equipment { EquipmentNumber = "EQ-CYCLE-01", Name = "循环设备" };
        db.AddRange(firstProject, secondProject, equipment);
        await db.SaveChangesAsync();
        var service = new ProjectConstructionService(db, new EquipmentService(db), new BusinessPartnerService(db));
        var actor = new ProjectConstructionActor("project-manager", "项目经理");
        var first = await service.SaveAsync(actor,
            new SaveProjectConstructionRecordRequest(null, firstProject.Id, ProjectConstructionRecordType.Equipment, equipment.Id, null, null, null,
                new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20), 0, null, false, null, "新增循环测试记录"),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        var missingDate = () => service.LinkNextAsync(actor,
            new LinkProjectConstructionRecordRequest(first.Id, secondProject.Id, first.ConcurrencyStamp, "缺少进场日期"),
            new DateOnly(2026, 7, 21), CancellationToken.None);
        await missingDate.Should().ThrowAsync<ArgumentException>().WithMessage("*进场日期*");

        await service.LinkNextAsync(actor,
            new LinkProjectConstructionRecordRequest(first.Id, secondProject.Id, first.ConcurrencyStamp, "建立正向流转", new DateOnly(2026, 7, 21)),
            new DateOnly(2026, 7, 21), CancellationToken.None);
        var second = await db.ProjectConstructionRecords.AsNoTracking().SingleAsync(item => item.ProjectId == secondProject.Id);
        var cycle = () => service.LinkNextAsync(actor,
            new LinkProjectConstructionRecordRequest(second.Id, firstProject.Id, second.ConcurrencyStamp, "尝试形成循环", new DateOnly(2026, 8, 1)),
            new DateOnly(2026, 7, 21), CancellationToken.None);

        await cycle.Should().ThrowAsync<InvalidOperationException>().WithMessage("*不能形成循环*");
    }
}
