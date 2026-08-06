using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Partners;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Tests.Application;

public sealed class BusinessPartnerServiceTests
{
    [Fact]
    public async Task ProjectLinkNotesRoundTripAndEnterAuditLog()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest("BP-LINK", "关联单位", "关联", null, null, [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, null, null, null)], []),
            CancellationToken.None);
        var project = new Project { ProjectNumber = "P-LINK", Name = "关联项目" };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.LinkToProjectAsync(
            new LinkPartnerToProjectRequest(partner.Id, project.Id, BusinessPartnerRoleType.ConstructionCrew, null, true, "合作备注"),
            CancellationToken.None);

        (await fixture.Db.ProjectPartners.SingleAsync()).Notes.Should().Be("合作备注");
        var audit = await fixture.Db.AuditLogs.SingleAsync(item => item.EntityType == nameof(ProjectPartner));
        using var after = JsonDocument.Parse(audit.AfterJson!);
        after.RootElement.GetProperty("Notes").GetString().Should().Be("合作备注");
    }

    [Fact]
    public async Task OnePartnerCanHaveMultipleRolesWithoutDuplicateMasterRecords()
    {
        await using var fixture = await PartnerFixture.CreateAsync();

        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-SVC-01",
                "综合合作单位",
                "综合单位",
                null,
                "测试单位",
                [
                    new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "土建", "工程量计价", null),
                    new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "辅材", "含税到场价", null)
                ],
                [new PartnerContactRequest("联系人", "13800000000", null, null, true)]),
            CancellationToken.None);

        partner.Roles.Should().HaveCount(2);
        (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DuplicatePartnerNumberIsRejected()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var request = new CreateBusinessPartnerRequest("BP-DUP", "单位一", "单位一", null, null, [], []);
        await fixture.Service.CreateAsync(request, CancellationToken.None);

        var action = () => fixture.Service.CreateAsync(request with { Name = "单位二" }, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*合作单位编号*");
    }

    [Fact]
    public async Task CopyKeepsReusableRoleSettingsButClearsContactsAndHistory()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var source = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-COPY-SRC",
                "原施工班组",
                "原班组",
                "913000000000000001",
                "常用班组",
                [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "安装", "按清单计价", "月度结算")],
                [new PartnerContactRequest("原联系人", "13900000000", null, null, true)]),
            CancellationToken.None);

        var copy = await fixture.Service.CopyAsync(
            new CopyBusinessPartnerRequest(source.Id, "BP-COPY-NEW", "新施工班组", "新班组"),
            CancellationToken.None);

        copy.Roles.Should().ContainSingle().Which.TradeCategory.Should().Be("安装");
        copy.Contacts.Should().BeEmpty();
        copy.UnifiedSocialCreditCode.Should().BeNull();
        copy.ProjectCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateChangesMasterDataAndPreservesAuditTrail()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(new CreateBusinessPartnerRequest(
            "BP-UPD",
            "原单位",
            "原单位",
            null,
            null,
            [
                new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "土建", null, null),
                new PartnerRoleRequest(BusinessPartnerRoleType.MiscellaneousSupplier, "设备", null, null)
            ],
            []), CancellationToken.None);

        var updated = await fixture.Service.UpdateAsync("admin", new UpdateBusinessPartnerRequest(
            partner.Id,
            partner.PartnerNumber,
            "修改后单位",
            "修改后",
            null,
            "更新备注",
            new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "材料", null, null),
            new PartnerContactRequest("新联系人", "13800000002", null, null, true, "联系人备注"),
            true,
            partner.ConcurrencyStamp,
            "维护合作单位",
            BusinessPartnerRoleType.ConstructionCrew), CancellationToken.None);

        updated.Name.Should().Be("修改后单位");
        updated.Roles.Should().NotContain(item => item.RoleType == BusinessPartnerRoleType.ConstructionCrew);
        updated.Roles.Should().Contain(item => item.RoleType == BusinessPartnerRoleType.MaterialSupplier);
        updated.Roles.Should().Contain(item => item.RoleType == BusinessPartnerRoleType.MiscellaneousSupplier);
        updated.Contacts.Should().ContainSingle().Which.Notes.Should().Be("联系人备注");
        (await fixture.Db.AuditLogs.SingleAsync()).Action.Should().Be("UpdateBusinessPartner");
    }

    [Fact]
    public async Task ChangingRoleUpdatesProjectLinksSoDirectorySyncDoesNotRestoreTheOldClassification()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-ROLE-SYNC",
                "角色联动单位",
                "角色联动",
                null,
                null,
                [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "土建", null, null)],
                []),
            CancellationToken.None);
        var project = new Project { ProjectNumber = "P-ROLE-SYNC", Name = "角色联动项目" };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.LinkToProjectAsync(
            new LinkPartnerToProjectRequest(
                partner.Id,
                project.Id,
                BusinessPartnerRoleType.ConstructionCrew,
                null,
                true,
                "原施工班组关联"),
            CancellationToken.None);

        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "材料", null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "调整合作单位分类",
                BusinessPartnerRoleType.ConstructionCrew),
            CancellationToken.None);
        await new BusinessPartnerDirectorySynchronizer(fixture.Db).SynchronizeAsync(project.Id, CancellationToken.None);

        (await fixture.Db.BusinessPartnerRoles.Where(item => item.BusinessPartnerId == partner.Id).Select(item => item.RoleType).ToListAsync())
            .Should().Equal(BusinessPartnerRoleType.MaterialSupplier);
        var projectLink = await fixture.Db.ProjectPartners.SingleAsync(item => item.BusinessPartnerId == partner.Id);
        projectLink.RoleType.Should().Be(BusinessPartnerRoleType.MaterialSupplier);
        projectLink.Notes.Should().Be("原施工班组关联");
    }

    [Fact]
    public async Task ConstructionCrewReferencedByProjectRecordsCannotBeReclassified()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-CREW-IN-USE",
                "在用施工班组",
                "在用班组",
                null,
                null,
                [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "土建", null, null)],
                []),
            CancellationToken.None);
        var project = new Project { ProjectNumber = "P-CREW-IN-USE", Name = "引用班组项目" };
        project.ConstructionRecords.Add(new ProjectConstructionRecord
        {
            Project = project,
            RecordType = ProjectConstructionRecordType.ConstructionCrew,
            CrewBusinessPartnerId = partner.Id
        });
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "材料", null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "调整合作单位分类",
                BusinessPartnerRoleType.ConstructionCrew),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("该施工班组仍被项目施工记录引用，无法改为其他类型，请先调整项目施工记录。");
        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.BusinessPartnerRoles.SingleAsync(item => item.BusinessPartnerId == partner.Id)).RoleType
            .Should().Be(BusinessPartnerRoleType.ConstructionCrew);
        (await fixture.Db.ProjectConstructionRecords.SingleAsync()).CrewBusinessPartnerId.Should().Be(partner.Id);
    }

    [Fact]
    public async Task ConstructionCrewWithPersonnelHistoryCannotBeReclassified()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-CREW-WORKERS",
                "已有人员施工班组",
                "人员班组",
                null,
                null,
                [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, "土建", null, null)],
                []),
            CancellationToken.None);
        var worker = new ConstructionWorker { Name = "历史班组人员" };
        worker.Memberships.Add(new ConstructionCrewMembership
        {
            Worker = worker,
            CrewBusinessPartnerId = partner.Id,
            StartDate = new DateOnly(2026, 1, 1),
            IsPrimary = true
        });
        fixture.Db.ConstructionWorkers.Add(worker);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "材料", null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "调整合作单位分类",
                BusinessPartnerRoleType.ConstructionCrew),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("该施工班组已有人员或历史归属，无法改为其他类型，请先调整人员归属。");
        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.BusinessPartnerRoles.SingleAsync(item => item.BusinessPartnerId == partner.Id)).RoleType
            .Should().Be(BusinessPartnerRoleType.ConstructionCrew);
    }

    [Fact]
    public async Task ReclassificationRejectsProjectLinkMergeWhenBothNotesDiffer()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-LINK-NOTES",
                "备注冲突单位",
                "备注冲突",
                null,
                null,
                [
                    new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, null, null, null),
                    new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, null, null, null)
                ],
                []),
            CancellationToken.None);
        var project = new Project { ProjectNumber = "P-LINK-NOTES", Name = "备注冲突项目" };
        project.Partners.Add(new ProjectPartner
        {
            Project = project,
            BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.ConstructionCrew,
            Notes = "施工班组备注"
        });
        project.Partners.Add(new ProjectPartner
        {
            Project = project,
            BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.MaterialSupplier,
            Notes = "材料供应备注"
        });
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                "不应保存的新名称",
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, null, null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "调整合作单位分类",
                BusinessPartnerRoleType.ConstructionCrew),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("同一项目的原角色与目标角色备注不同，无法合并，请先统一备注。");
        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.BusinessPartners.SingleAsync(item => item.Id == partner.Id)).Name.Should().Be("备注冲突单位");
        var links = await fixture.Db.ProjectPartners.Where(item => item.BusinessPartnerId == partner.Id).OrderBy(item => item.RoleType).ToArrayAsync();
        links.Should().HaveCount(2);
        links.Select(item => item.Notes).Should().BeEquivalentTo(["施工班组备注", "材料供应备注"]);
    }

    [Fact]
    public async Task ReclassificationRejectsProjectLinkMergeWhenBothContractsDiffer()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-LINK-CONTRACTS",
                "合同冲突单位",
                "合同冲突",
                null,
                null,
                [
                    new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, null, null, null),
                    new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, null, null, null)
                ],
                []),
            CancellationToken.None);
        var project = new Project { ProjectNumber = "P-LINK-CONTRACTS", Name = "合同冲突项目" };
        var sourceContract = new Contract { Project = project, ContractNumber = "C-SOURCE", Name = "原角色合同" };
        var targetContract = new Contract { Project = project, ContractNumber = "C-TARGET", Name = "目标角色合同" };
        project.Partners.Add(new ProjectPartner
        {
            Project = project,
            BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.ConstructionCrew,
            Contract = sourceContract
        });
        project.Partners.Add(new ProjectPartner
        {
            Project = project,
            BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.MaterialSupplier,
            Contract = targetContract
        });
        fixture.Db.Projects.Add(project);
        fixture.Db.Contracts.AddRange(sourceContract, targetContract);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, null, null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "调整合作单位分类",
                BusinessPartnerRoleType.ConstructionCrew),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("同一项目的原角色与目标角色关联了不同合同，无法合并，请先统一合同。");
        fixture.Db.ChangeTracker.Clear();
        var contractIds = await fixture.Db.ProjectPartners
            .Where(item => item.BusinessPartnerId == partner.Id)
            .OrderBy(item => item.RoleType)
            .Select(item => item.ContractId)
            .ToArrayAsync();
        contractIds.Should().BeEquivalentTo([sourceContract.Id, targetContract.Id]);
    }

    [Fact]
    public async Task ReclassificationMergesCompatibleProjectLinksWithoutLosingData()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-LINK-MERGE",
                "可合并关联单位",
                "可合并关联",
                null,
                null,
                [
                    new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, null, null, null),
                    new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, null, null, null)
                ],
                []),
            CancellationToken.None);
        var project = new Project { ProjectNumber = "P-LINK-MERGE", Name = "可合并关联项目" };
        var contract = new Contract { Project = project, ContractNumber = "C-MERGE", Name = "保留合同" };
        project.Partners.Add(new ProjectPartner
        {
            Project = project,
            BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.ConstructionCrew,
            Contract = contract,
            IsActive = true,
            Notes = "保留关联备注"
        });
        project.Partners.Add(new ProjectPartner
        {
            Project = project,
            BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.MaterialSupplier,
            IsPrimary = true,
            IsActive = false
        });
        fixture.Db.Projects.Add(project);
        fixture.Db.Contracts.Add(contract);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, null, null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "合并兼容项目关联",
                BusinessPartnerRoleType.ConstructionCrew),
            CancellationToken.None);

        var link = await fixture.Db.ProjectPartners.SingleAsync(item => item.BusinessPartnerId == partner.Id);
        link.RoleType.Should().Be(BusinessPartnerRoleType.MaterialSupplier);
        link.ContractId.Should().Be(contract.Id);
        link.Notes.Should().Be("保留关联备注");
        link.IsPrimary.Should().BeTrue();
        link.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ManualReclassificationOfAutoSyncedGeneralContractorSurvivesLaterProjectSynchronization()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-GC-OVERRIDE",
                "可改类总包单位",
                "可改类总包",
                null,
                null,
                [new PartnerRoleRequest(BusinessPartnerRoleType.CustomerOrGeneralContractor, null, null, null)],
                []),
            CancellationToken.None);
        var project = new Project
        {
            ProjectNumber = "P-GC-OVERRIDE",
            Name = "总包分类调整项目",
            GeneralContractorName = partner.Name
        };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        var synchronizer = new BusinessPartnerDirectorySynchronizer(fixture.Db);
        await synchronizer.SynchronizeAsync(project.Id, CancellationToken.None);

        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "材料", null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "人工调整总包分类",
                BusinessPartnerRoleType.CustomerOrGeneralContractor),
            CancellationToken.None);
        await synchronizer.SynchronizeAsync(project.Id, CancellationToken.None);

        (await fixture.Db.BusinessPartnerRoles.Where(item => item.BusinessPartnerId == partner.Id).Select(item => item.RoleType).ToListAsync())
            .Should().Equal(BusinessPartnerRoleType.MaterialSupplier);
        (await fixture.Db.ProjectPartners.Where(item => item.BusinessPartnerId == partner.Id).Select(item => item.RoleType).ToListAsync())
            .Should().Equal(BusinessPartnerRoleType.MaterialSupplier);
    }

    [Fact]
    public async Task RenamingAutoSyncedGeneralContractorUpdatesProjectSourceWithoutCreatingDuplicateMasterData()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var project = new Project
        {
            ProjectNumber = "P-GC-RENAME",
            Name = "总包主档改名项目",
            GeneralContractorName = ProjectGeneralContractors.Serialize(["旧总包有限公司", "并列总包有限公司"])
        };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        var synchronizer = new BusinessPartnerDirectorySynchronizer(fixture.Db);
        await synchronizer.SynchronizeAsync(project.Id, CancellationToken.None);
        var partner = await fixture.Db.BusinessPartners
            .Include(item => item.Roles)
            .SingleAsync(item => item.Name == "旧总包有限公司");

        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                "新总包有限公司",
                "新总包",
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.CustomerOrGeneralContractor, null, null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "修正总包单位名称",
                BusinessPartnerRoleType.CustomerOrGeneralContractor),
            CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var updatedProject = await fixture.Db.Projects.SingleAsync(item => item.Id == project.Id);
        ProjectGeneralContractors.Parse(updatedProject.GeneralContractorName)
            .Should().Equal("新总包有限公司", "并列总包有限公司");

        await synchronizer.SynchronizeAsync(project.Id, CancellationToken.None);

        (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(2);
        (await fixture.Db.BusinessPartners.CountAsync(item => item.Name == "旧总包有限公司")).Should().Be(0);
        (await fixture.Db.BusinessPartners.CountAsync(item => item.Name == "新总包有限公司")).Should().Be(1);
        (await fixture.Db.ProjectPartners.CountAsync(item => item.ProjectId == project.Id)).Should().Be(2);
    }

    [Fact]
    public async Task EditingAutoSyncedGeneralContractorWithoutRenamingPreservesProjectShortNameSource()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-GC-ALIAS",
                "别名总包有限公司",
                "别名总包",
                null,
                null,
                [new PartnerRoleRequest(BusinessPartnerRoleType.CustomerOrGeneralContractor, null, null, null)],
                []),
            CancellationToken.None);
        var project = new Project
        {
            ProjectNumber = "P-GC-ALIAS",
            Name = "总包别名项目",
            GeneralContractorName = partner.ShortName
        };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        await new BusinessPartnerDirectorySynchronizer(fixture.Db).SynchronizeAsync(project.Id, CancellationToken.None);

        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                "仅更新备注",
                new PartnerRoleRequest(BusinessPartnerRoleType.CustomerOrGeneralContractor, null, null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "维护总包备注",
                BusinessPartnerRoleType.CustomerOrGeneralContractor),
            CancellationToken.None);

        (await fixture.Db.Projects.SingleAsync(item => item.Id == project.Id)).GeneralContractorName
            .Should().Be("别名总包");
    }

    [Fact]
    public async Task RenamingOnlyAutoSyncedGeneralContractorShortNameUpdatesShortNameSource()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-GC-SHORT",
                "简称变更总包有限公司",
                "旧简称总包",
                null,
                null,
                [new PartnerRoleRequest(BusinessPartnerRoleType.CustomerOrGeneralContractor, null, null, null)],
                []),
            CancellationToken.None);
        var project = new Project
        {
            ProjectNumber = "P-GC-SHORT",
            Name = "总包简称变更项目",
            GeneralContractorName = partner.ShortName
        };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        await new BusinessPartnerDirectorySynchronizer(fixture.Db).SynchronizeAsync(project.Id, CancellationToken.None);

        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                "新简称总包",
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.CustomerOrGeneralContractor, null, null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "修正总包简称",
                BusinessPartnerRoleType.CustomerOrGeneralContractor),
            CancellationToken.None);

        (await fixture.Db.Projects.SingleAsync(item => item.Id == project.Id)).GeneralContractorName
            .Should().Be("新简称总包");
    }

    [Fact]
    public async Task PureBusinessPartnerPersonnelDoesNotBlockRemovingConstructionCrewRole()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-CREW-PARTNER-PERSON",
                "兼有普通外部人员的单位",
                "普通外部人员单位",
                null,
                null,
                [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, null, null, null)],
                []),
            CancellationToken.None);
        var person = new Person { PersonNumber = "PER-PARTNER-ONLY", Name = "普通合作单位人员" };
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.External,
            ExternalType = ExternalPersonnelType.BusinessPartner,
            BusinessPartnerId = partner.Id,
            StartDate = new DateOnly(2026, 1, 1),
            IsPrimary = true,
            Reason = "普通合作单位归属"
        });
        fixture.Db.People.Add(person);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(
                partner.Id,
                partner.PartnerNumber,
                partner.Name,
                partner.ShortName,
                partner.UnifiedSocialCreditCode,
                partner.Notes,
                new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, null, null, null),
                null,
                true,
                partner.ConcurrencyStamp,
                "调整单位类型",
                BusinessPartnerRoleType.ConstructionCrew),
            CancellationToken.None);

        (await fixture.Db.BusinessPartnerRoles.SingleAsync(item => item.BusinessPartnerId == partner.Id)).RoleType
            .Should().Be(BusinessPartnerRoleType.MaterialSupplier);
        (await fixture.Db.PersonnelEngagementHistories.SingleAsync()).BusinessPartnerId.Should().Be(partner.Id);
    }

    [Fact]
    public async Task PartnerProjectCountIncludesActiveConstructionRecordProjectsWithoutDoubleCountingLinks()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest(
                "BP-CREW-COUNT",
                "项目计数班组",
                "计数班组",
                null,
                null,
                [new PartnerRoleRequest(BusinessPartnerRoleType.ConstructionCrew, null, null, null)],
                []),
            CancellationToken.None);
        var recordOnlyProject = new Project { ProjectNumber = "P-CREW-COUNT-1", Name = "仅施工记录项目" };
        recordOnlyProject.ConstructionRecords.Add(new ProjectConstructionRecord
        {
            Project = recordOnlyProject,
            RecordType = ProjectConstructionRecordType.ConstructionCrew,
            CrewBusinessPartnerId = partner.Id
        });
        var linkedProject = new Project { ProjectNumber = "P-CREW-COUNT-2", Name = "关联与施工记录重复项目" };
        linkedProject.ConstructionRecords.Add(new ProjectConstructionRecord
        {
            Project = linkedProject,
            RecordType = ProjectConstructionRecordType.ConstructionCrew,
            CrewBusinessPartnerId = partner.Id
        });
        linkedProject.Partners.Add(new ProjectPartner
        {
            Project = linkedProject,
            BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.ConstructionCrew
        });
        fixture.Db.Projects.AddRange(recordOnlyProject, linkedProject);
        await fixture.Db.SaveChangesAsync();

        var listed = await fixture.Service.ListForManagementAsync(null, null, CancellationToken.None);

        listed.Single(item => item.Id == partner.Id).ProjectCount.Should().Be(2);
    }

    [Fact]
    public async Task PartnerSearchUsesContactRoleAndNotesWithAndSemantics()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest("BP-SEARCH", "全字段单位", "全字段", "913000000000000001", "单位备注",
                [new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "防水工程", "按量计价", "月结")],
                [new PartnerContactRequest("联系人甲", "13900000000", "search@example.test", "单位地址", true, "联系人备注")]),
            CancellationToken.None);

        (await fixture.Service.ListAsync("联系人甲 防水工程 单位备注", null, CancellationToken.None)).Should().ContainSingle(item => item.Id == partner.Id);
        (await fixture.Service.ListAsync("联系人甲 不存在", null, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ManagementListIncludesInactivePartnersWithoutChangingActiveOnlyList()
    {
        await using var fixture = await PartnerFixture.CreateAsync();
        var partner = await fixture.Service.CreateAsync(
            new CreateBusinessPartnerRequest("BP-INACTIVE", "停用合作单位", "停用单位", null, null, [new PartnerRoleRequest(BusinessPartnerRoleType.MaterialSupplier, "钢材", null, null)], []),
            CancellationToken.None);
        var role = partner.Roles.Single();
        await fixture.Service.UpdateAsync(
            "admin",
            new UpdateBusinessPartnerRequest(partner.Id, partner.PartnerNumber, partner.Name, partner.ShortName, null, null, new PartnerRoleRequest(role.RoleType, role.TradeCategory, role.PricingRule, role.SettlementTerms), null, false, partner.ConcurrencyStamp, "停用合作单位"),
            CancellationToken.None);

        (await fixture.Service.ListAsync(null, null, CancellationToken.None)).Should().BeEmpty();
        (await fixture.Service.ListForManagementAsync(null, null, CancellationToken.None))
            .Should().ContainSingle(item => item.Id == partner.Id && !item.IsActive);
    }

    private sealed class PartnerFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private PartnerFixture(SqliteConnection connection, ApplicationDbContext db, IBusinessPartnerService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IBusinessPartnerService Service { get; }

        public static async Task<PartnerFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new PartnerFixture(connection, db, new BusinessPartnerService(db));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
