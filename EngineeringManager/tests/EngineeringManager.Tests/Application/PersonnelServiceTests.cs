using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Personnel;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class PersonnelServiceTests
{
    [Fact]
    public async Task CreateInternalPersonBuildsOnePersonEmployeeAndCurrentEngagement()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();

        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-001", "内部人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            PositionTitle: "项目经理",
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "新增内部人员"), CancellationToken.None);

        created.EmployeeId.Should().NotBeNull();
        created.CurrentAffiliation!.Scope.Should().Be(PersonnelScope.Internal);
        (await fixture.Db.People.CountAsync()).Should().Be(1);
        var employee = await fixture.Db.Employees.Include(item => item.AffiliationHistory).SingleAsync();
        employee.PersonId.Should().Be(created.Id);
        employee.AffiliationHistory.Should().ContainSingle();
        employee.AffiliationHistory.Single().Should().BeEquivalentTo(new
        {
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = (DateOnly?)null,
            LegalEntityId = (Guid?)fixture.Company.Id,
            PositionTitle = "项目经理",
            IsPrimary = true
        });
        (await fixture.Db.PersonnelEngagementHistories.SingleAsync()).LegalEntityId.Should().Be(fixture.Company.Id);
    }

    [Fact]
    public async Task ListFiltersInternalAndExternalPeopleByCurrentCrew()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync("CREW-FILTER", "筛选班组");
        var otherCrew = await fixture.AddCrewAsync("CREW-OTHER", "其他班组");
        await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-CREW-INTERNAL", "内部派驻人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            CrewBusinessPartnerId: crew.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "内部派驻"), CancellationToken.None);
        await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-CREW-EXTERNAL", "外部班组人员", PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            BusinessPartnerId: crew.Id,
            CrewBusinessPartnerId: crew.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "外部班组"), CancellationToken.None);
        await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-CREW-DECOY", "其他班组人员", PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            BusinessPartnerId: otherCrew.Id,
            CrewBusinessPartnerId: otherCrew.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "其他班组"), CancellationToken.None);

        var internalPeople = await fixture.Service.ListAsync(
            new PersonnelListQuery(PersonnelScope.Internal, CrewBusinessPartnerId: crew.Id),
            false,
            CancellationToken.None);
        var externalPeople = await fixture.Service.ListAsync(
            new PersonnelListQuery(PersonnelScope.External, CrewBusinessPartnerId: crew.Id),
            false,
            CancellationToken.None);

        internalPeople.Should().ContainSingle().Which.PersonNumber.Should().Be("P-CREW-INTERNAL");
        externalPeople.Should().ContainSingle().Which.PersonNumber.Should().Be("P-CREW-EXTERNAL");
    }

    [Fact]
    public async Task LaterAffiliationClosesOldRecordAndBecomesCurrentProject()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var oldProject = await fixture.AddProjectAsync("P-OLD", "旧项目");
        var newProject = await fixture.AddProjectAsync("P-NEW", "新项目");
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-002", "调动人员", PersonnelScope.Internal, EmployeeType.Labor, null,
            LegalEntityId: fixture.Company.Id,
            ProjectId: oldProject.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始归属"), CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Formal, null,
            fixture.Company.Id, null, null, newProject.Id, null, "施工员",
            new DateOnly(2026, 6, 1), "调整项目"), CancellationToken.None);

        var details = await fixture.Service.GetAsync(created.Id, new DateOnly(2026, 8, 6), true, CancellationToken.None);
        var history = await fixture.Db.PersonnelEngagementHistories.OrderBy(item => item.StartDate).ToArrayAsync();
        var employeeHistory = await fixture.Db.EmployeeAffiliationHistories.OrderBy(item => item.StartDate).ToArrayAsync();
        details!.CurrentAffiliation!.ProjectId.Should().Be(newProject.Id);
        history.Should().HaveCount(2);
        history[0].EndDate.Should().Be(new DateOnly(2026, 5, 31));
        employeeHistory.Should().HaveCount(2);
        employeeHistory[0].EndDate.Should().Be(new DateOnly(2026, 5, 31));
        employeeHistory[1].ProjectId.Should().Be(newProject.Id);
        var employee = await fixture.Db.Employees.SingleAsync();
        employee.EmployeeType.Should().Be(EmployeeType.Formal);
        employee.DefaultLegalEntityId.Should().Be(fixture.Company.Id);
        employee.PositionTitle.Should().Be("施工员");
    }

    [Fact]
    public async Task SameDayAffiliationCorrectionUpdatesExistingRecordsInsteadOfCreatingOverlaps()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var oldProject = await fixture.AddProjectAsync("P-SAME-OLD", "同日旧项目");
        var newProject = await fixture.AddProjectAsync("P-SAME-NEW", "同日新项目");
        var effectiveDate = new DateOnly(2026, 1, 1);
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-SAME-DAY", "同日纠错人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            ProjectId: oldProject.Id,
            PositionTitle: "原岗位",
            EffectiveDate: effectiveDate,
            Reason: "初始归属"), CancellationToken.None);

        var corrected = await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Labor, null,
            fixture.Company.Id, null, null, newProject.Id, null, "新岗位",
            effectiveDate, "同日录入纠错", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        corrected.ProjectId.Should().Be(newProject.Id);
        corrected.InternalType.Should().Be(EmployeeType.Labor);
        corrected.PositionTitle.Should().Be("新岗位");
        (await fixture.Db.PersonnelEngagementHistories.ToArrayAsync()).Should().ContainSingle()
            .Which.EndDate.Should().BeNull();
        var employeeHistory = await fixture.Db.EmployeeAffiliationHistories.ToArrayAsync();
        employeeHistory.Should().ContainSingle();
        employeeHistory[0].ProjectId.Should().Be(newProject.Id);
        employeeHistory[0].PositionTitle.Should().Be("新岗位");
        employeeHistory[0].EndDate.Should().BeNull();
        var audit = await fixture.Db.AuditLogs.SingleAsync(item => item.Action == "UpdatePersonnelAffiliation");
        audit.BeforeJson.Should().Contain($"\"ProjectId\":\"{oldProject.Id}\"")
            .And.Contain("\"EndDate\":null");
    }

    [Fact]
    public async Task LaterAffiliationAuditKeepsTheOriginalOpenEndedSnapshot()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-AUDIT-BEFORE", "归属审计人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始归属"), CancellationToken.None);

        await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Labor, null,
            fixture.Company.Id, null, null, null, null, "施工员",
            new DateOnly(2026, 6, 1), "后续归属调整", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        var audit = await fixture.Db.AuditLogs.SingleAsync(item => item.Action == "UpdatePersonnelAffiliation");
        audit.BeforeJson.Should().Contain("\"EndDate\":null");
    }

    [Fact]
    public async Task SameDayCrewCorrectionMovesTheExistingMembershipInPlace()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var oldCrew = await fixture.AddCrewAsync("CREW-SAME-OLD", "同日原班组");
        var newCrew = await fixture.AddCrewAsync("CREW-SAME-NEW", "同日新班组");
        var effectiveDate = new DateOnly(2026, 1, 1);
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-SAME-CREW", "同日班组纠错", PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            BusinessPartnerId: oldCrew.Id,
            CrewBusinessPartnerId: oldCrew.Id,
            PositionTitle: "木工",
            EffectiveDate: effectiveDate,
            Reason: "初始班组归属"), CancellationToken.None);

        await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, newCrew.Id, null, null, newCrew.Id, "瓦工",
            effectiveDate, "同日班组纠错", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        var engagement = await fixture.Db.PersonnelEngagementHistories.SingleAsync();
        engagement.BusinessPartnerId.Should().Be(newCrew.Id);
        engagement.CrewBusinessPartnerId.Should().Be(newCrew.Id);
        engagement.EndDate.Should().BeNull();
        var membership = await fixture.Db.ConstructionCrewMemberships.SingleAsync();
        membership.CrewBusinessPartnerId.Should().Be(newCrew.Id);
        membership.EndDate.Should().BeNull();
        membership.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task SwitchToExternalCrewCreatesWorkerMembershipAndKeepsEmployeeBridge()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-003", "转外人员", PersonnelScope.Internal, EmployeeType.Temporary, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);

        var switched = await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 7, 1), "转为班组人员", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        switched.EmployeeId.Should().NotBeNull();
        switched.ConstructionWorkerId.Should().NotBeNull();
        var employee = await fixture.Db.Employees.SingleAsync();
        employee.IsActive.Should().BeFalse();
        employee.LeaveDate.Should().Be(new DateOnly(2026, 6, 30));
        var employeeHistory = await fixture.Db.EmployeeAffiliationHistories.OrderBy(item => item.StartDate).ToArrayAsync();
        employeeHistory.Should().ContainSingle();
        employeeHistory[0].EndDate.Should().Be(new DateOnly(2026, 6, 30));
        (await fixture.Db.ConstructionWorkers.SingleAsync()).PersonId.Should().Be(created.Id);
        (await fixture.Db.ConstructionCrewMemberships.SingleAsync()).CrewBusinessPartnerId.Should().Be(crew.Id);
        (await fixture.Db.PersonnelEngagementHistories.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SavingPublicDataAfterSwitchToExternalDoesNotReactivateHistoricalEmployee()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-PUBLIC-SCOPE-1", "公共资料切换人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);

        var switched = await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 7, 1), "转为外部班组人员", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await fixture.Service.SavePublicDataAsync("admin", new SavePersonRequest(
            switched.Id, "公共资料切换后", switched.Phone, switched.IdentityNumber, switched.BankAccountNumber,
            switched.BankName, switched.Notes, true, switched.ConcurrencyStamp, "更新公共资料"), CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.People.SingleAsync()).IsActive.Should().BeTrue();
        (await fixture.Db.Employees.SingleAsync()).IsActive.Should().BeFalse();
        (await fixture.Db.ConstructionWorkers.SingleAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SwitchFromExternalCrewToInternalClosesCrewPayrollIdentity()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-004", "转内人员", PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            BusinessPartnerId: crew.Id,
            CrewBusinessPartnerId: crew.Id,
            PositionTitle: "钢筋工",
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始班组身份"), CancellationToken.None);

        var switched = await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Formal, null,
            fixture.Company.Id, null, null, null, null, "施工员",
            new DateOnly(2026, 7, 1), "转为内部人员", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        switched.EmployeeId.Should().NotBeNull();
        var worker = await fixture.Db.ConstructionWorkers.SingleAsync();
        worker.IsActive.Should().BeFalse();
        var membership = await fixture.Db.ConstructionCrewMemberships.SingleAsync();
        membership.EndDate.Should().Be(new DateOnly(2026, 6, 30));
        membership.IsPrimary.Should().BeFalse();
        (await fixture.Db.Employees.SingleAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SavingPublicDataAfterSwitchToInternalDoesNotReactivateHistoricalWorker()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-PUBLIC-SCOPE-2", "公共资料切换人员2", PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            BusinessPartnerId: crew.Id,
            CrewBusinessPartnerId: crew.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始班组身份"), CancellationToken.None);

        var switched = await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Formal, null,
            fixture.Company.Id, null, null, null, null, "施工员",
            new DateOnly(2026, 7, 1), "转为内部人员", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await fixture.Service.SavePublicDataAsync("admin", new SavePersonRequest(
            switched.Id, "公共资料切换后2", switched.Phone, switched.IdentityNumber, switched.BankAccountNumber,
            switched.BankName, switched.Notes, true, switched.ConcurrencyStamp, "更新公共资料"), CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.People.SingleAsync()).IsActive.Should().BeTrue();
        (await fixture.Db.Employees.SingleAsync()).IsActive.Should().BeTrue();
        (await fixture.Db.ConstructionWorkers.SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SwitchingInactivePersonDoesNotActivateTargetProfile()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-INACTIVE-SWITCH", "停用后切换人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);
        var disabled = await fixture.Service.SavePublicDataAsync("admin", new SavePersonRequest(
            created.Id, created.Name, created.Phone, created.IdentityNumber, created.BankAccountNumber,
            created.BankName, created.Notes, false, created.ConcurrencyStamp, "停用人员"), CancellationToken.None);

        await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 7, 1), "停用人员切换身份", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        (await fixture.Db.People.SingleAsync()).IsActive.Should().BeFalse();
        (await fixture.Db.Employees.SingleAsync()).IsActive.Should().BeFalse();
        (await fixture.Db.ConstructionWorkers.SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task LaterExternalCrewAffiliationMovesPrimaryMembershipToTheNewCrew()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var oldCrew = await fixture.AddCrewAsync("CREW-OLD", "原班组");
        var newCrew = await fixture.AddCrewAsync("CREW-NEW", "新班组");
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-007", "班组调动人员", PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            BusinessPartnerId: oldCrew.Id,
            CrewBusinessPartnerId: oldCrew.Id,
            PositionTitle: "木工",
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始班组归属"), CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, newCrew.Id, null, null, newCrew.Id, "瓦工",
            new DateOnly(2026, 7, 1), "调整施工班组"), CancellationToken.None);

        var memberships = await fixture.Db.ConstructionCrewMemberships.OrderBy(item => item.StartDate).ToArrayAsync();
        memberships.Should().HaveCount(2);
        memberships[0].CrewBusinessPartnerId.Should().Be(oldCrew.Id);
        memberships[0].EndDate.Should().Be(new DateOnly(2026, 6, 30));
        memberships[0].IsPrimary.Should().BeFalse();
        memberships[1].CrewBusinessPartnerId.Should().Be(newCrew.Id);
        memberships[1].EndDate.Should().BeNull();
        memberships[1].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAffiliationCannotLeaveConstructionCrewTypeWithoutIdentitySwitch()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync("CREW-LEAVE-TYPE", "退出班组类型测试");
        var partner = await fixture.AddPartnerAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-LEAVE-CREW-TYPE", "退出班组类型人员", PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            BusinessPartnerId: crew.Id,
            CrewBusinessPartnerId: crew.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始班组身份"), CancellationToken.None);

        var action = () => fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.BusinessPartner,
            null, partner.Id, null, null, null, null,
            new DateOnly(2026, 7, 1), "尝试直接改变外部类型", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("施工班组人员类型变更请使用身份切换功能。");
        var worker = await fixture.Db.ConstructionWorkers.Include(item => item.Memberships).SingleAsync();
        worker.IsActive.Should().BeTrue();
        worker.Memberships.Should().ContainSingle(item => item.IsPrimary && !item.EndDate.HasValue);
    }

    [Fact]
    public async Task SaveAffiliationCannotEnterConstructionCrewTypeWithoutIdentitySwitch()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var partner = await fixture.AddPartnerAsync();
        var crew = await fixture.AddCrewAsync("CREW-ENTER-TYPE", "进入班组类型测试");
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-ENTER-CREW-TYPE", "进入班组类型人员", PersonnelScope.External, null, ExternalPersonnelType.BusinessPartner,
            BusinessPartnerId: partner.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始合作单位身份"), CancellationToken.None);

        var action = () => fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 7, 1), "尝试直接进入班组类型", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("施工班组人员类型变更请使用身份切换功能。");
        (await fixture.Db.ConstructionWorkers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveAffiliationCannotBypassInternalExternalScopeSwitchAtLaterDate()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var partner = await fixture.AddPartnerAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-BYPASS-SCOPE", "范围绕过人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);

        var action = () => fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.BusinessPartner,
            null, partner.Id, null, null, null, null,
            new DateOnly(2026, 7, 1), "尝试绕过身份切换", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("人员范围变更请使用内部 / 外部身份切换功能。");
        (await fixture.Db.PersonnelEngagementHistories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SwitchingInternalExternalInternalKeepsOnlyInternalEmployeeAffiliations()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-008", "往返切换人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            PositionTitle: "项目经理",
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);

        var external = await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 5, 1), "转为外部班组人员", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);
        await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Labor, null,
            fixture.Company.Id, null, null, null, null, "施工员",
            new DateOnly(2026, 7, 1), "转回内部人员", external.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        var employee = await fixture.Db.Employees.Include(item => item.AffiliationHistory).SingleAsync();
        var history = employee.AffiliationHistory.OrderBy(item => item.StartDate).ToArray();
        history.Should().HaveCount(2);
        history[0].StartDate.Should().Be(new DateOnly(2026, 1, 1));
        history[0].EndDate.Should().Be(new DateOnly(2026, 4, 30));
        history[1].StartDate.Should().Be(new DateOnly(2026, 7, 1));
        history[1].EndDate.Should().BeNull();
        employee.IsActive.Should().BeTrue();
        employee.LeaveDate.Should().BeNull();
        employee.EmployeeType.Should().Be(EmployeeType.Labor);
        employee.PositionTitle.Should().Be("施工员");
        employee.DefaultLegalEntityId.Should().Be(fixture.Company.Id);
    }

    [Fact]
    public async Task EditingInternalAffiliationDoesNotReactivateInactivePersonnel()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-009", "停用内部人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            PositionTitle: "项目经理",
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);
        await fixture.Service.SavePublicDataAsync("admin", new SavePersonRequest(
            created.Id, created.Name, created.Phone, created.IdentityNumber, created.BankAccountNumber,
            created.BankName, created.Notes, false, created.ConcurrencyStamp, "停用人员"), CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Labor, null,
            fixture.Company.Id, null, null, null, null, "施工员",
            new DateOnly(2026, 7, 1), "调整内部归属"), CancellationToken.None);

        (await fixture.Db.People.SingleAsync()).IsActive.Should().BeFalse();
        (await fixture.Db.Employees.SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task EditingCrewAffiliationDoesNotReactivateInactivePersonnel()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-010", "停用班组人员", PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            BusinessPartnerId: crew.Id,
            CrewBusinessPartnerId: crew.Id,
            PositionTitle: "木工",
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始班组身份"), CancellationToken.None);
        await fixture.Service.SavePublicDataAsync("admin", new SavePersonRequest(
            created.Id, created.Name, created.Phone, created.IdentityNumber, created.BankAccountNumber,
            created.BankName, created.Notes, false, created.ConcurrencyStamp, "停用人员"), CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "瓦工",
            new DateOnly(2026, 7, 1), "调整班组归属"), CancellationToken.None);

        (await fixture.Db.People.SingleAsync()).IsActive.Should().BeFalse();
        (await fixture.Db.ConstructionWorkers.SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SwitchingInternalPersonnelToExternalRefreshesEmployeeConcurrencyStamp()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-011", "并发切换人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);
        var originalStamp = (await fixture.Db.Employees.SingleAsync()).ConcurrencyStamp;

        await fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 7, 1), "转为外部人员", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        var employee = await fixture.Db.Employees.SingleAsync();
        employee.IsActive.Should().BeFalse();
        employee.ConcurrencyStamp.Should().NotBe(originalStamp);
    }

    [Fact]
    public async Task ScopeSwitchRejectsStaleCurrentAffiliationConcurrencyStamp()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-STALE-SWITCH", "并发归属人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);
        var staleStamp = created.CurrentAffiliation!.ConcurrencyStamp;

        await fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Labor, null,
            fixture.Company.Id, null, null, null, null, "施工员",
            new DateOnly(2026, 6, 1), "其他用户调整归属", staleStamp), CancellationToken.None);

        var action = () => fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 7, 1), "使用旧页面切换", staleStamp), CancellationToken.None);

        await action.Should().ThrowAsync<DbUpdateConcurrencyException>()
            .WithMessage("人员归属已被其他用户修改，请刷新后重试。");
        fixture.Db.ChangeTracker.Clear();
        var current = await fixture.Service.GetAsync(created.Id, new DateOnly(2026, 7, 1), true, CancellationToken.None);
        current!.CurrentAffiliation!.Scope.Should().Be(PersonnelScope.Internal);
        (await fixture.Db.ConstructionWorkers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RejectedScopeSwitchWithInvalidCrewLeavesTrackedBusinessProfilesUntouched()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-012", "无效班组切换", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);
        var employee = await fixture.Db.Employees.SingleAsync();
        var originalStamp = employee.ConcurrencyStamp;
        var invalidCrewId = Guid.NewGuid();

        var action = () => fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, invalidCrewId, null, null, invalidCrewId, "木工",
            new DateOnly(2026, 7, 1), "尝试无效切换", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("所属合作单位不存在或已停用。");
        employee.IsActive.Should().BeTrue();
        employee.LeaveDate.Should().BeNull();
        employee.ConcurrencyStamp.Should().Be(originalStamp);
        fixture.Db.ChangeTracker.Entries<ConstructionWorker>().Should().BeEmpty();
        fixture.Db.ChangeTracker.Entries<ConstructionCrewMembership>().Should().BeEmpty();
    }

    [Fact]
    public async Task RejectedScopeSwitchWithInvalidEffectiveDateLeavesTrackedBusinessProfilesUntouched()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync();
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-013", "无效日期切换", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);
        var employee = await fixture.Db.Employees.SingleAsync();
        var originalStamp = employee.ConcurrencyStamp;

        var action = () => fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            new DateOnly(2026, 1, 1), "尝试无效日期切换", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("新归属生效日期必须晚于当前归属开始日期。");
        employee.IsActive.Should().BeTrue();
        employee.LeaveDate.Should().BeNull();
        employee.ConcurrencyStamp.Should().Be(originalStamp);
        fixture.Db.ChangeTracker.Entries<ConstructionWorker>().Should().BeEmpty();
        fixture.Db.ChangeTracker.Entries<ConstructionCrewMembership>().Should().BeEmpty();
    }

    [Fact]
    public async Task FutureScopeSwitchIsRejectedBeforeBusinessProfilesChange()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var crew = await fixture.AddCrewAsync("CREW-FUTURE", "未来切换班组");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-FUTURE-SWITCH", "未来切换人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: today.AddDays(-1),
            Reason: "初始内部身份"), CancellationToken.None);
        var employee = await fixture.Db.Employees.SingleAsync();
        var originalStamp = employee.ConcurrencyStamp;

        var action = () => fixture.Service.SwitchScopeAsync("admin", new SwitchPersonnelScopeRequest(
            created.Id, PersonnelScope.External, null, ExternalPersonnelType.ConstructionCrew,
            null, crew.Id, null, null, crew.Id, "木工",
            today.AddDays(1), "未来生效切换", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("归属生效日期不能晚于今天。");
        employee.IsActive.Should().BeTrue();
        employee.LeaveDate.Should().BeNull();
        employee.ConcurrencyStamp.Should().Be(originalStamp);
        fixture.Db.ChangeTracker.Entries<ConstructionWorker>().Should().BeEmpty();
        fixture.Db.ChangeTracker.Entries<ConstructionCrewMembership>().Should().BeEmpty();
    }

    [Fact]
    public async Task SelectedCrewMustBelongToTheSelectedProject()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var project = await fixture.AddProjectAsync("P-CREW-LINK", "班组联动项目");
        var crew = await fixture.AddCrewAsync("CREW-NOT-LINKED", "未关联班组");
        var created = await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-CREW-LINK", "班组联动人员", PersonnelScope.Internal, EmployeeType.Formal, null,
            LegalEntityId: fixture.Company.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "初始内部身份"), CancellationToken.None);

        var action = () => fixture.Service.SaveAffiliationAsync("admin", new SavePersonnelAffiliationRequest(
            created.Id, PersonnelScope.Internal, EmployeeType.Formal, null,
            fixture.Company.Id, null, null, project.Id, crew.Id, "施工员",
            new DateOnly(2026, 7, 1), "选择未关联班组", created.CurrentAffiliation!.ConcurrencyStamp), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("所选施工班组未关联当前项目。");
    }

    [Fact]
    public async Task ExternalBusinessPartnerPersonRequiresExactlyOnePartnerOwner()
    {
        await using var fixture = await PersonnelFixture.CreateAsync();
        var partner = await fixture.AddPartnerAsync();

        var missingOwner = async () => await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-005", "无单位外部人员", PersonnelScope.External, null, ExternalPersonnelType.BusinessPartner,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "验证外部归属"), CancellationToken.None);
        var conflictingOwners = async () => await fixture.Service.CreateAsync("admin", new CreatePersonRequest(
            "P-006", "双重归属外部人员", PersonnelScope.External, null, ExternalPersonnelType.BusinessPartner,
            LegalEntityId: fixture.Company.Id,
            BusinessPartnerId: partner.Id,
            EffectiveDate: new DateOnly(2026, 1, 1),
            Reason: "验证归属互斥"), CancellationToken.None);

        await missingOwner.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("外部人员必须选择所属合作单位。");
        await conflictingOwners.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("外部人员不能同时归属自有公司和合作单位。");
    }

    private sealed class PersonnelFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private PersonnelFixture(SqliteConnection connection, ApplicationDbContext db, IPersonnelService service, LegalEntity company)
        {
            this.connection = connection;
            Db = db;
            Service = service;
            Company = company;
        }

        public ApplicationDbContext Db { get; }
        public IPersonnelService Service { get; }
        public LegalEntity Company { get; }

        public static async Task<PersonnelFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var company = new LegalEntity { Code = "PERSONNEL-LE", Name = "人员测试公司", ShortName = "人员公司" };
            db.LegalEntities.Add(company);
            await db.SaveChangesAsync();
            return new PersonnelFixture(connection, db, new PersonnelService(db), company);
        }

        public async Task<Project> AddProjectAsync(string number, string name)
        {
            var project = new Project { ProjectNumber = number, Name = name, Stage = ProjectStage.UnderConstruction };
            project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntityId = Company.Id, IsPrimary = true });
            Db.Projects.Add(project);
            await Db.SaveChangesAsync();
            return project;
        }

        public async Task<BusinessPartner> AddCrewAsync(string number = "CREW-001", string name = "人员测试班组")
        {
            var crew = new BusinessPartner { PartnerNumber = number, Name = name, ShortName = name };
            crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
            Db.BusinessPartners.Add(crew);
            await Db.SaveChangesAsync();
            return crew;
        }

        public async Task<BusinessPartner> AddPartnerAsync()
        {
            var partner = new BusinessPartner { PartnerNumber = "PARTNER-001", Name = "人员测试合作单位", ShortName = "测试合作单位" };
            partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = BusinessPartnerRoleType.MaterialSupplier });
            Db.BusinessPartners.Add(partner);
            await Db.SaveChangesAsync();
            return partner;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
