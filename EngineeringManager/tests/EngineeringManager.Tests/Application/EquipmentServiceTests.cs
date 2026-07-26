using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Equipment;
using EngineeringManager.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Tests.Application;

public sealed class EquipmentServiceTests
{
    [Fact]
    public async Task QualificationAttachmentCanBeUploadedAndDownloaded()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-F", Name = "设备附件公司", ShortName = "附件公司" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();
        var content = new byte[] { 1, 3, 5, 7 };

        var saved = await scope.Service.SaveEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            new SaveEquipmentRequest(
                null,
                "EQ-FILE",
                "附件设备",
                null,
                null,
                EquipmentOwnershipType.SelfOwned,
                company.Id,
                null,
                null,
                null,
                "上传合格证",
                ManagingLegalEntityId: company.Id,
                QualificationCertificateNumber: "QC-FILE",
                NewQualificationAttachment: new CertificateAttachmentUpload("设备合格证.pdf", "application/pdf", content)),
            default);

        saved.QualificationAttachmentId.Should().NotBeNull();
        saved.QualificationAttachmentFileName.Should().Be("设备合格证.pdf");
        var file = await scope.Service.DownloadQualificationAttachmentAsync(
            EquipmentActor.Administrator("admin"), saved.Id, default);
        file.OriginalFileName.Should().Be("设备合格证.pdf");
        file.Content.Should().Equal(content);
    }

    [Fact]
    public async Task InvalidQualificationReplacementKeepsExistingAttachment()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-FR", Name = "附件替换公司", ShortName = "替换公司" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();
        var actor = EquipmentActor.Administrator("admin");
        var saved = await scope.Service.SaveEquipmentAsync(
            actor,
            new SaveEquipmentRequest(
                null,
                "EQ-REPLACE",
                "附件替换设备",
                null,
                null,
                EquipmentOwnershipType.SelfOwned,
                company.Id,
                null,
                null,
                null,
                "上传原附件",
                ManagingLegalEntityId: company.Id,
                NewQualificationAttachment: new CertificateAttachmentUpload("原合格证.pdf", "application/pdf", [2, 4, 6])),
            default);

        var replace = () => scope.Service.SaveEquipmentAsync(
            actor,
            new SaveEquipmentRequest(
                saved.Id,
                saved.EquipmentNumber,
                saved.Name,
                saved.Model,
                saved.Category,
                saved.OwnershipType,
                saved.OwnerLegalEntityId,
                saved.LessorBusinessPartnerId,
                saved.InternalDailyRate,
                saved.ConcurrencyStamp,
                "替换附件",
                saved.Notes,
                saved.ManagingLegalEntityId,
                saved.PurchaseDate,
                saved.PurchaseAmount,
                saved.QualificationCertificateNumber,
                saved.QualificationIssuedOn,
                saved.QualificationExpiresOn,
                new CertificateAttachmentUpload("恶意文件.exe", "application/octet-stream", [9]),
                false,
                saved.IsActive),
            default);

        await replace.Should().ThrowAsync<ArgumentException>().WithMessage("*类型不受支持*");
        var original = await scope.Service.DownloadQualificationAttachmentAsync(actor, saved.Id, default);
        original.OriginalFileName.Should().Be("原合格证.pdf");
        original.Content.Should().Equal(2, 4, 6);
    }

    [Fact]
    public async Task QualificationAttachmentCanBeRemovedWithoutClearingCertificateMetadata()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-FD", Name = "附件删除公司", ShortName = "删除公司" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();
        var actor = EquipmentActor.Administrator("admin");
        var saved = await scope.Service.SaveEquipmentAsync(
            actor,
            new SaveEquipmentRequest(
                null,
                "EQ-DELETE-FILE",
                "附件删除设备",
                null,
                null,
                EquipmentOwnershipType.SelfOwned,
                company.Id,
                null,
                null,
                null,
                "上传附件",
                ManagingLegalEntityId: company.Id,
                QualificationCertificateNumber: "QC-KEEP",
                NewQualificationAttachment: new CertificateAttachmentUpload("待删除.pdf", "application/pdf", [8, 6, 4])),
            default);

        var updated = await scope.Service.SaveEquipmentAsync(
            actor,
            new SaveEquipmentRequest(
                saved.Id,
                saved.EquipmentNumber,
                saved.Name,
                saved.Model,
                saved.Category,
                saved.OwnershipType,
                saved.OwnerLegalEntityId,
                saved.LessorBusinessPartnerId,
                saved.InternalDailyRate,
                saved.ConcurrencyStamp,
                "删除附件",
                saved.Notes,
                saved.ManagingLegalEntityId,
                saved.PurchaseDate,
                saved.PurchaseAmount,
                saved.QualificationCertificateNumber,
                saved.QualificationIssuedOn,
                saved.QualificationExpiresOn,
                RemoveQualificationAttachment: true,
                IsActive: saved.IsActive),
            default);

        updated.QualificationCertificateNumber.Should().Be("QC-KEEP");
        updated.QualificationAttachmentId.Should().BeNull();
        (await scope.Db.Attachments.IgnoreQueryFilters().SingleAsync(item => item.Id == saved.QualificationAttachmentId)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task EquipmentMasterDataRoundTripsWithManagingCompanyAndQualification()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-M", Name = "设备管理公司", ShortName = "管理公司" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();

        var saved = await scope.Service.SaveEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            new SaveEquipmentRequest(
                null,
                "EQ-MASTER",
                "完整档案设备",
                "M-100",
                "起重设备",
                EquipmentOwnershipType.SelfOwned,
                company.Id,
                null,
                900m,
                null,
                "新增完整档案",
                "设备档案",
                ManagingLegalEntityId: company.Id,
                PurchaseDate: new DateOnly(2026, 5, 1),
                PurchaseAmount: 350000m,
                QualificationCertificateNumber: "QC-2026-001",
                QualificationIssuedOn: new DateOnly(2026, 5, 2),
                QualificationExpiresOn: new DateOnly(2027, 5, 1)),
            default);

        saved.ManagingLegalEntityId.Should().Be(company.Id);
        saved.ManagingLegalEntityName.Should().Be("设备管理公司");
        saved.OwnerLegalEntityName.Should().Be("设备管理公司");
        saved.PurchaseDate.Should().Be(new DateOnly(2026, 5, 1));
        saved.PurchaseAmount.Should().Be(350000m);
        saved.QualificationCertificateNumber.Should().Be("QC-2026-001");
        saved.QualificationIssuedOn.Should().Be(new DateOnly(2026, 5, 2));
        saved.QualificationExpiresOn.Should().Be(new DateOnly(2027, 5, 1));
    }

    [Fact]
    public async Task EquipmentNotesRoundTripAndEnterAuditLog()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-N", Name = "设备备注公司", ShortName = "设备备注" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();

        var saved = await scope.Service.SaveEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            new SaveEquipmentRequest(null, "EQ-NOTES", "备注设备", null, null, EquipmentOwnershipType.SelfOwned, company.Id, null, null, null, "新增", "设备备注", ManagingLegalEntityId: company.Id),
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

        var equipment = await scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "EQ-001", "测试挖机", "X1", "挖掘机", EquipmentOwnershipType.SelfOwned, company.Id, null, 500m, null, "新增", ManagingLegalEntityId: company.Id), default);
        var copy = await scope.Service.CopyEquipmentAsync(actor, equipment.Id, default);
        var usage = await scope.Service.SaveUsageAsync(actor, new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), RentMode.Daily, MonthlyProrationMode.ThirtyDay, 500m, false, null, [new EquipmentPeriodRequest(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), EquipmentPeriodType.Work, true, null)], null, "进场"), default);

        copy.EquipmentNumber.Should().BeEmpty();
        copy.Name.Should().Contain("副本");
        copy.ManagingLegalEntityId.Should().Be(company.Id);
        copy.ManagingLegalEntityName.Should().Be("设备公司");
        copy.OwnerLegalEntityName.Should().Be("设备公司");
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
        var company = new LegalEntity { Code = "EQ-R", Name = "设备规则公司", ShortName = "规则公司" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();
        var missingManager = () => scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "M", "待分配", null, null, EquipmentOwnershipType.SelfOwned, company.Id, null, null, null, "测试"), default);
        var selfOwned = () => scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "A", "自有", null, null, EquipmentOwnershipType.SelfOwned, null, null, null, null, "测试", ManagingLegalEntityId: company.Id), default);
        var rented = () => scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "B", "租赁", null, null, EquipmentOwnershipType.Rented, null, null, null, null, "测试", ManagingLegalEntityId: company.Id), default);
        await missingManager.Should().ThrowAsync<ArgumentException>().WithMessage("*管理公司*");
        await selfOwned.Should().ThrowAsync<ArgumentException>().WithMessage("*所属公司*");
        await rented.Should().ThrowAsync<ArgumentException>().WithMessage("*出租方*");
    }

    [Fact]
    public async Task OtherEquipmentCanBeSavedWithoutOwnerOrLessor()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-OTHER", Name = "其他设备公司", ShortName = "其他设备" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();

        var saved = await scope.Service.SaveEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            new SaveEquipmentRequest(
                null,
                "EQ-OTHER-001",
                "其他来源设备",
                null,
                null,
                EquipmentOwnershipType.Other,
                null,
                null,
                null,
                null,
                "新增其他设备",
                ManagingLegalEntityId: company.Id),
            default);

        saved.OwnershipType.Should().Be(EquipmentOwnershipType.Other);
        saved.OwnerLegalEntityId.Should().BeNull();
        saved.LessorBusinessPartnerId.Should().BeNull();
    }

    [Fact]
    public async Task UsageHistoryReturnsOnlyRecordsOverlappingSelectedBusinessYear()
    {
        await using var scope = await CreateScopeAsync();
        var actor = EquipmentActor.Administrator("admin");
        var company = new LegalEntity { Code = "EQ-YEAR", Name = "业务年公司", ShortName = "业务年" };
        var project = new Project { ProjectNumber = "EQ-YEAR-P", Name = "业务年项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        scope.Db.AddRange(company, project);
        await scope.Db.SaveChangesAsync();
        var equipment = await scope.Service.SaveEquipmentAsync(
            actor,
            new SaveEquipmentRequest(null, "EQ-YEAR-001", "业务年设备", null, null, EquipmentOwnershipType.Other, null, null, 680m, null, "新增", ManagingLegalEntityId: company.Id),
            default);
        var included = await scope.Service.SaveUsageAsync(
            actor,
            new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2025, 12, 20), new DateOnly(2026, 1, 10), RentMode.Daily, MonthlyProrationMode.ThirtyDay, 680m, false, null, [new EquipmentPeriodRequest(new DateOnly(2025, 12, 20), new DateOnly(2026, 1, 10), EquipmentPeriodType.Work, true, "跨年施工")], null, "跨年进退场"),
            default);
        await scope.Service.SaveUsageAsync(
            actor,
            new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 5), RentMode.Daily, MonthlyProrationMode.ThirtyDay, 500m, false, null, [new EquipmentPeriodRequest(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 5), EquipmentPeriodType.Work, true, null)], null, "往年进退场"),
            default);

        var history = await scope.Service.ListUsagesAsync(
            actor,
            new EquipmentUsageFilter(equipment.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            default);

        history.Should().ContainSingle();
        history[0].Id.Should().Be(included.Id);
        history[0].EquipmentName.Should().Be("业务年设备");
        history[0].ProjectName.Should().Be("业务年项目");
        history[0].LegalEntityName.Should().Be("业务年公司");
        history[0].Periods.Should().ContainSingle(period => period.Notes == "跨年施工");
    }

    [Fact]
    public async Task EditingClosedHistoryKeepsEquipmentInUseWhenAnotherUsageIsOpen()
    {
        await using var scope = await CreateScopeAsync();
        var actor = EquipmentActor.Administrator("admin");
        var company = new LegalEntity { Code = "EQ-STATUS", Name = "设备状态公司", ShortName = "设备状态" };
        var project = new Project { ProjectNumber = "EQ-STATUS-P", Name = "设备状态项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        scope.Db.AddRange(company, project);
        await scope.Db.SaveChangesAsync();
        var equipment = await scope.Service.SaveEquipmentAsync(
            actor,
            new SaveEquipmentRequest(null, "EQ-STATUS-001", "状态设备", null, null, EquipmentOwnershipType.Other, null, null, 300m, null, "新增", ManagingLegalEntityId: company.Id),
            default);
        var closed = await scope.Service.SaveUsageAsync(
            actor,
            new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 5), RentMode.Daily, MonthlyProrationMode.ThirtyDay, 300m, false, null, [new EquipmentPeriodRequest(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 5), EquipmentPeriodType.Work, true, null)], null, "历史记录"),
            default);
        await scope.Service.SaveUsageAsync(
            actor,
            new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2026, 1, 1), null, RentMode.Daily, MonthlyProrationMode.ThirtyDay, 300m, false, null, [], null, "当前在场"),
            default);
        scope.Db.ChangeTracker.Clear();

        await scope.Service.SaveUsageAsync(
            actor,
            new SaveEquipmentUsageRequest(closed.Id, equipment.Id, project.Id, company.Id, null, new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 5), RentMode.Daily, MonthlyProrationMode.ThirtyDay, 320m, false, null, [new EquipmentPeriodRequest(new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 5), EquipmentPeriodType.Work, true, null)], closed.ConcurrencyStamp, "编辑历史记录"),
            default);

        (await scope.Db.Equipment.SingleAsync(item => item.Id == equipment.Id)).Status.Should().Be(EquipmentStatus.InUse);
    }

    [Fact]
    public async Task ExistingUsageCannotBeMovedToAnotherEquipment()
    {
        await using var scope = await CreateScopeAsync();
        var actor = EquipmentActor.Administrator("admin");
        var company = new LegalEntity { Code = "EQ-MOVE", Name = "设备迁移公司", ShortName = "设备迁移" };
        var project = new Project { ProjectNumber = "EQ-MOVE-P", Name = "设备迁移项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        scope.Db.AddRange(company, project);
        await scope.Db.SaveChangesAsync();
        var source = await scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "EQ-MOVE-1", "原设备", null, null, EquipmentOwnershipType.Other, null, null, 300m, null, "新增", ManagingLegalEntityId: company.Id), default);
        var target = await scope.Service.SaveEquipmentAsync(actor, new SaveEquipmentRequest(null, "EQ-MOVE-2", "目标设备", null, null, EquipmentOwnershipType.Other, null, null, 300m, null, "新增", ManagingLegalEntityId: company.Id), default);
        var usage = await scope.Service.SaveUsageAsync(actor, new SaveEquipmentUsageRequest(null, source.Id, project.Id, company.Id, null, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), RentMode.Daily, MonthlyProrationMode.ThirtyDay, 300m, false, null, [new EquipmentPeriodRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), EquipmentPeriodType.Work, true, null)], null, "新增使用"), default);

        var move = () => scope.Service.SaveUsageAsync(actor, new SaveEquipmentUsageRequest(usage.Id, target.Id, project.Id, company.Id, null, usage.EntryDate, usage.ExitDate, RentMode.Daily, MonthlyProrationMode.ThirtyDay, 300m, false, null, [new EquipmentPeriodRequest(usage.EntryDate, usage.ExitDate!.Value, EquipmentPeriodType.Work, true, null)], usage.ConcurrencyStamp, "尝试迁移"), default);

        await move.Should().ThrowAsync<InvalidOperationException>().WithMessage("*不能变更设备*");
        (await scope.Db.EquipmentProjectUsages.AsNoTracking().SingleAsync(item => item.Id == usage.Id)).EquipmentId.Should().Be(source.Id);
    }

    [Fact]
    public async Task UsageHistoryExcludesProjectsOutsideTheActorScope()
    {
        await using var scope = await CreateScopeAsync();
        var administrator = EquipmentActor.Administrator("admin");
        var company = new LegalEntity { Code = "EQ-SCOPE", Name = "设备范围公司", ShortName = "设备范围" };
        var project = new Project { ProjectNumber = "EQ-SCOPE-P", Name = "无权项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        scope.Db.AddRange(company, project);
        await scope.Db.SaveChangesAsync();
        var equipment = await scope.Service.SaveEquipmentAsync(administrator, new SaveEquipmentRequest(null, "EQ-SCOPE-1", "范围设备", null, null, EquipmentOwnershipType.Other, null, null, 200m, null, "新增", ManagingLegalEntityId: company.Id), default);
        await scope.Service.SaveUsageAsync(administrator, new SaveEquipmentUsageRequest(null, equipment.Id, project.Id, company.Id, null, new DateOnly(2026, 5, 1), null, RentMode.Daily, MonthlyProrationMode.ThirtyDay, 200m, false, null, [], null, "进场"), default);
        var scopedActor = new EquipmentActor("scoped", true, false, false, false, [company.Id], []);

        var history = await scope.Service.ListUsagesAsync(scopedActor, new EquipmentUsageFilter(equipment.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), default);

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task InactiveCompaniesCannotOwnOrManageNewEquipment()
    {
        await using var scope = await CreateScopeAsync();
        var inactiveCompany = new LegalEntity
        {
            Code = "EQ-INACTIVE",
            Name = "停用设备公司",
            ShortName = "停用公司",
            IsActive = false
        };
        scope.Db.LegalEntities.Add(inactiveCompany);
        await scope.Db.SaveChangesAsync();

        var save = () => scope.Service.SaveEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            new SaveEquipmentRequest(
                null,
                "EQ-INACTIVE",
                "停用公司设备",
                null,
                null,
                EquipmentOwnershipType.SelfOwned,
                inactiveCompany.Id,
                null,
                null,
                null,
                "停用公司校验",
                ManagingLegalEntityId: inactiveCompany.Id),
            default);

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*不存在或无权访问*");
    }

    [Fact]
    public async Task EquipmentSearchUsesAllMasterAndRelatedFields()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-SEARCH-C", Name = "设备搜索公司", ShortName = "设备搜索" };
        var lessor = new BusinessPartner { PartnerNumber = "EQ-SEARCH-L", Name = "设备出租方", ShortName = "出租方" };
        scope.Db.AddRange(company, lessor);
        await scope.Db.SaveChangesAsync();
        await scope.Service.SaveEquipmentAsync(EquipmentActor.Administrator("admin"), new SaveEquipmentRequest(null, "EQ-SEARCH", "全字段设备", "M-SEARCH", "塔吊", EquipmentOwnershipType.Rented, null, lessor.Id, 800m, null, "新增", "设备备注", ManagingLegalEntityId: company.Id), default);

        var result = await scope.Service.GetDashboardAsync(EquipmentActor.Administrator("admin"), new EquipmentFilter(null, null, null, "M-SEARCH 设备出租方 设备备注"), default);

        result.Items.Should().ContainSingle(item => item.EquipmentNumber == "EQ-SEARCH");
    }

    [Fact]
    public async Task DashboardCompanyScopeUsesManagingCompanyAndKeepsUnassignedSeparate()
    {
        await using var scope = await CreateScopeAsync();
        var managingCompany = new LegalEntity { Code = "EQ-MANAGER", Name = "设备管理公司", ShortName = "管理" };
        var ownerCompany = new LegalEntity { Code = "EQ-OWNER", Name = "设备产权公司", ShortName = "产权" };
        scope.Db.AddRange(managingCompany, ownerCompany);
        await scope.Db.SaveChangesAsync();
        await scope.Service.SaveEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            new SaveEquipmentRequest(null, "EQ-SCOPED", "代管设备", null, null, EquipmentOwnershipType.SelfOwned, ownerCompany.Id, null, null, null, "新增", ManagingLegalEntityId: managingCompany.Id),
            default);
        scope.Db.Equipment.Add(new Equipment
        {
            EquipmentNumber = "EQ-UNASSIGNED",
            Name = "历史待分配设备",
            OwnershipType = EquipmentOwnershipType.SelfOwned,
            OwnerLegalEntityId = ownerCompany.Id
        });
        await scope.Db.SaveChangesAsync();

        var managed = await scope.Service.GetDashboardAsync(EquipmentActor.Administrator("admin"), new EquipmentFilter(managingCompany.Id, null, null, null), default);
        var owned = await scope.Service.GetDashboardAsync(EquipmentActor.Administrator("admin"), new EquipmentFilter(ownerCompany.Id, null, null, null), default);
        var unassigned = await scope.Service.GetDashboardAsync(EquipmentActor.Administrator("admin"), new EquipmentFilter(null, null, null, null, true), default);

        managed.Items.Should().ContainSingle(item => item.EquipmentNumber == "EQ-SCOPED");
        owned.Items.Should().BeEmpty();
        unassigned.Items.Should().ContainSingle(item => item.EquipmentNumber == "EQ-UNASSIGNED");
    }

    [Fact]
    public async Task EquipmentStatusCanBeChangedFromTheEditor()
    {
        await using var scope = await CreateScopeAsync();
        var actor = EquipmentActor.Administrator("admin");
        var company = new LegalEntity { Code = "EQ-EDIT-STATUS", Name = "状态编辑公司", ShortName = "状态编辑" };
        scope.Db.LegalEntities.Add(company);
        await scope.Db.SaveChangesAsync();
        var equipment = await scope.Service.SaveEquipmentAsync(
            actor,
            new SaveEquipmentRequest(null, "EQ-EDIT-STATUS-001", "状态设备", null, null, EquipmentOwnershipType.Other, null, null, null, null, "新增", ManagingLegalEntityId: company.Id),
            default);

        var updated = await scope.Service.SaveEquipmentAsync(
            actor,
            new SaveEquipmentRequest(equipment.Id, equipment.EquipmentNumber, equipment.Name, equipment.Model, equipment.Category, equipment.OwnershipType, equipment.OwnerLegalEntityId, equipment.LessorBusinessPartnerId, equipment.InternalDailyRate, equipment.ConcurrencyStamp, "调整设备状态", ManagingLegalEntityId: company.Id, Status: EquipmentStatus.Maintenance),
            default);

        updated.Status.Should().Be(EquipmentStatus.Maintenance);
    }

    [Fact]
    public async Task EquipmentWithoutBusinessRecordsCanBePhysicallyDeleted()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-DELETE", Name = "设备删除公司", ShortName = "删除公司" };
        var equipment = new Equipment
        {
            EquipmentNumber = "EQ-DELETE-001",
            Name = "待删除设备",
            ManagingLegalEntity = company,
            OwnershipType = EquipmentOwnershipType.Other
        };
        scope.Db.Add(equipment);
        await scope.Db.SaveChangesAsync();

        await scope.Service.DeleteEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            equipment.Id,
            equipment.ConcurrencyStamp,
            equipment.EquipmentNumber,
            "删除无业务设备",
            default);

        (await scope.Db.Equipment.AnyAsync(item => item.Id == equipment.Id)).Should().BeFalse();
        (await scope.Db.AuditLogs.AnyAsync(item => item.EntityType == nameof(Equipment) && item.Action == "Delete")).Should().BeTrue();
    }

    [Fact]
    public async Task EquipmentDeletionRequiresTheExactEquipmentNumber()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-CONFIRM", Name = "删除确认公司", ShortName = "确认公司" };
        var equipment = new Equipment
        {
            EquipmentNumber = "EQ-CONFIRM-001",
            Name = "确认设备",
            ManagingLegalEntity = company,
            OwnershipType = EquipmentOwnershipType.Other
        };
        scope.Db.Add(equipment);
        await scope.Db.SaveChangesAsync();

        var delete = () => scope.Service.DeleteEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            equipment.Id,
            equipment.ConcurrencyStamp,
            "WRONG-NUMBER",
            "测试二次确认",
            default);

        await delete.Should().ThrowAsync<InvalidOperationException>().WithMessage("*设备编号*");
        (await scope.Db.Equipment.AnyAsync(item => item.Id == equipment.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task EquipmentWithBusinessRecordsCannotBePhysicallyDeleted()
    {
        await using var scope = await CreateScopeAsync();
        var company = new LegalEntity { Code = "EQ-PROTECTED", Name = "删除保护公司", ShortName = "保护公司" };
        var equipment = new Equipment
        {
            EquipmentNumber = "EQ-PROTECTED-001",
            Name = "有关联设备",
            ManagingLegalEntity = company,
            OwnershipType = EquipmentOwnershipType.Other
        };
        scope.Db.AddRange(equipment, new EquipmentMaintenanceRecord
        {
            Equipment = equipment,
            MaintenanceDate = new DateOnly(2026, 7, 1),
            MaintenanceType = "例行维护"
        });
        await scope.Db.SaveChangesAsync();

        var delete = () => scope.Service.DeleteEquipmentAsync(
            EquipmentActor.Administrator("admin"),
            equipment.Id,
            equipment.ConcurrencyStamp,
            equipment.EquipmentNumber,
            "测试关联保护",
            default);

        await delete.Should().ThrowAsync<InvalidOperationException>().WithMessage("*业务记录*");
        (await scope.Db.Equipment.AnyAsync(item => item.Id == equipment.Id)).Should().BeTrue();
    }

    private static async Task<TestScope> CreateScopeAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return new TestScope(connection, db, new EquipmentService(db, new MemoryFileStore()));
    }

    private sealed class TestScope(SqliteConnection connection, ApplicationDbContext db, EquipmentService service) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public EquipmentService Service { get; } = service;
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class MemoryFileStore : IFileStore
    {
        private readonly Dictionary<string, byte[]> files = [];

        public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken)
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
            files[storedName] = buffer.ToArray();
            return storedName;
        }

        public Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(new MemoryStream(files[storedName], writable: false));
        }

        public Task DeleteAsync(string storedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Remove(storedName);
            return Task.CompletedTask;
        }
    }
}
