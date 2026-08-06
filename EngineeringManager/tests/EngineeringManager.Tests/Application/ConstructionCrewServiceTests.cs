using EngineeringManager.Application.ConstructionCrews;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.ConstructionCrews;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ConstructionCrewServiceTests
{
    [Fact]
    public async Task AddWorkerBuildsUnifiedPersonAndExternalEngagement()
    {
        await using var fixture = await CrewFixture.CreateAsync();

        var worker = await fixture.Service.AddWorkerAsync(
            "admin",
            new CreateConstructionWorkerRequest(fixture.FirstCrew.Id, "统一工人", null, null, null, null, "木工", new DateOnly(2026, 7, 1), null, "建立班组名册"),
            CancellationToken.None);

        var entity = await fixture.Db.ConstructionWorkers.Include(item => item.Person).SingleAsync(item => item.Id == worker.Id);
        entity.PersonId.Should().NotBeNull();
        entity.Person!.Name.Should().Be("统一工人");
        var engagement = await fixture.Db.PersonnelEngagementHistories.SingleAsync();
        engagement.BusinessPartnerId.Should().Be(fixture.FirstCrew.Id);
        engagement.CrewBusinessPartnerId.Should().Be(fixture.FirstCrew.Id);
    }

    [Fact]
    public async Task AddingWorkerForExistingInternalPersonRequiresUnifiedScopeSwitch()
    {
        await using var fixture = await CrewFixture.CreateAsync();
        const string identityNumber = "110101199001010099";
        var person = new Person
        {
            PersonNumber = "PER-INTERNAL",
            Name = "既有内部人员",
            IdentityNumber = identityNumber,
            IdentityNumberNormalized = identityNumber
        };
        person.Employee = new Employee
        {
            Person = person,
            EmployeeNumber = "E-INTERNAL",
            Name = person.Name,
            EmployeeType = EmployeeType.Formal,
            IdentityNumber = identityNumber
        };
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.Internal,
            InternalType = EmployeeType.Formal,
            StartDate = new DateOnly(2026, 1, 1),
            IsPrimary = true,
            Reason = "既有内部身份"
        });
        fixture.Db.People.Add(person);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.AddWorkerAsync(
            "admin",
            new CreateConstructionWorkerRequest(
                fixture.FirstCrew.Id,
                person.Name,
                identityNumber,
                null,
                null,
                null,
                "木工",
                new DateOnly(2026, 7, 1),
                null,
                "尝试绕过身份切换"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("该身份证号已存在内部人员档案，请在人员管理中使用身份切换。");
        (await fixture.Db.PersonnelEngagementHistories.CountAsync()).Should().Be(1);
        (await fixture.Db.ConstructionWorkers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddingWorkerForExistingExternalPartnerPersonRequiresUnifiedAffiliationSwitch()
    {
        await using var fixture = await CrewFixture.CreateAsync();
        const string identityNumber = "110101199001010088";
        var person = new Person
        {
            PersonNumber = "PER-EXTERNAL",
            Name = "既有合作单位人员",
            IdentityNumber = identityNumber,
            IdentityNumberNormalized = identityNumber
        };
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.External,
            ExternalType = ExternalPersonnelType.BusinessPartner,
            BusinessPartnerId = fixture.Supplier.Id,
            StartDate = new DateOnly(2026, 1, 1),
            IsPrimary = true,
            Reason = "既有合作单位归属"
        });
        fixture.Db.People.Add(person);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.AddWorkerAsync(
            "admin",
            new CreateConstructionWorkerRequest(
                fixture.FirstCrew.Id,
                person.Name,
                identityNumber,
                null,
                null,
                null,
                "木工",
                new DateOnly(2026, 7, 1),
                null,
                "尝试绕过外部归属切换"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("该身份证号已有生效或未来的人员归属，请在人员管理中使用身份切换。");
        (await fixture.Db.PersonnelEngagementHistories.CountAsync()).Should().Be(1);
        (await fixture.Db.ConstructionWorkers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CrewProjectCountIncludesConstructionRecordsWithoutDoubleCountingProjectLinks()
    {
        await using var fixture = await CrewFixture.CreateAsync();
        var recordOnlyProject = new Project { ProjectNumber = "CREW-PROJECT-COUNT-1", Name = "仅施工记录项目" };
        recordOnlyProject.ConstructionRecords.Add(new ProjectConstructionRecord
        {
            Project = recordOnlyProject,
            RecordType = ProjectConstructionRecordType.ConstructionCrew,
            CrewBusinessPartnerId = fixture.FirstCrew.Id
        });
        var linkedProject = new Project { ProjectNumber = "CREW-PROJECT-COUNT-2", Name = "重复来源项目" };
        linkedProject.ConstructionRecords.Add(new ProjectConstructionRecord
        {
            Project = linkedProject,
            RecordType = ProjectConstructionRecordType.ConstructionCrew,
            CrewBusinessPartnerId = fixture.FirstCrew.Id
        });
        linkedProject.Partners.Add(new ProjectPartner
        {
            Project = linkedProject,
            BusinessPartnerId = fixture.FirstCrew.Id,
            RoleType = BusinessPartnerRoleType.ConstructionCrew
        });
        fixture.Db.Projects.AddRange(recordOnlyProject, linkedProject);
        await fixture.Db.SaveChangesAsync();

        var crews = await fixture.Service.ListAsync(false, CancellationToken.None);

        crews.Single(item => item.Id == fixture.FirstCrew.Id).ProjectCount.Should().Be(2);
    }

    [Fact]
    public async Task CrewListUsesPartnerRoleAndWorkerCanTransferWithoutRewritingHistory()
    {
        await using var fixture = await CrewFixture.CreateAsync();

        var worker = await fixture.Service.AddWorkerAsync(
            "admin",
            new CreateConstructionWorkerRequest(fixture.FirstCrew.Id, "张三", null, "13800000000", null, null, "钢筋工", new DateOnly(2026, 7, 1), null, "建立班组名册"),
            CancellationToken.None);
        await fixture.Service.TransferWorkerAsync(
            "admin",
            new TransferConstructionWorkerRequest(worker.Id, fixture.SecondCrew.Id, new DateOnly(2026, 8, 1), "项目调班"),
            CancellationToken.None);

        var crews = await fixture.Service.ListAsync(false, CancellationToken.None);
        var memberships = await fixture.Db.ConstructionCrewMemberships.OrderBy(item => item.StartDate).ToListAsync();

        crews.Should().HaveCount(2);
        crews.Should().NotContain(item => item.Id == fixture.Supplier.Id);
        memberships.Should().HaveCount(2);
        memberships[0].EndDate.Should().Be(new DateOnly(2026, 7, 31));
        memberships[1].CrewBusinessPartnerId.Should().Be(fixture.SecondCrew.Id);
        (await fixture.Db.AuditLogs.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task IdentityNumberIsOptionalAndMaskedForNonSensitiveViewers()
    {
        await using var fixture = await CrewFixture.CreateAsync();
        await fixture.Service.AddWorkerAsync(
            "admin",
            new CreateConstructionWorkerRequest(fixture.FirstCrew.Id, "无证件工人", null, null, null, null, null, new DateOnly(2026, 7, 1), null, "建立班组名册"),
            CancellationToken.None);
        await fixture.Service.AddWorkerAsync(
            "admin",
            new CreateConstructionWorkerRequest(fixture.FirstCrew.Id, "有证件工人", "110101199001010011", null, null, null, null, new DateOnly(2026, 7, 1), null, "建立班组名册"),
            CancellationToken.None);

        var details = await fixture.Service.GetAsync(fixture.FirstCrew.Id, false, CancellationToken.None);

        details!.Workers.Should().Contain(item => item.Name == "无证件工人" && item.IdentityNumber == null);
        details.Workers.Should().Contain(item => item.Name == "有证件工人" && item.IdentityNumber == "110***********0011");
    }

    [Fact]
    public async Task CrewPaymentHistoryKeepsTheExactPeopleAndAmountsForEachBatch()
    {
        await using var fixture = await CrewFixture.CreateAsync();
        var first = await fixture.Service.AddWorkerAsync("admin", new CreateConstructionWorkerRequest(fixture.FirstCrew.Id, "张三", null, null, null, null, "钢筋工", new DateOnly(2026, 7, 1), null, "建立名册"), CancellationToken.None);
        var second = await fixture.Service.AddWorkerAsync("admin", new CreateConstructionWorkerRequest(fixture.FirstCrew.Id, "李四", null, null, null, null, "钢筋工", new DateOnly(2026, 7, 1), null, "建立名册"), CancellationToken.None);
        var project = new Project { ProjectNumber = "CREW-PAY-P", Name = "班组工资倒查项目", Stage = ProjectStage.UnderConstruction };
        var batch = new PayrollBatch
        {
            BatchNumber = "CREW-PAY-001",
            Name = "班组工资倒查批次",
            BatchType = PayrollBatchType.Temporary,
            StartDate = new DateOnly(2026, 7, 18),
            EndDate = new DateOnly(2026, 7, 18),
            PaymentDate = new DateOnly(2026, 7, 18),
            Project = project,
            ActualAmount = 7_000m,
            IsUnifiedDisbursement = true,
            Status = PayrollBatchStatus.Confirmed
        };
        batch.Payments.Add(new PayrollPayment { Batch = batch, RecipientType = PayrollRecipientType.CrewWorker, RecipientKey = $"crew:{first.Id:N}", ConstructionWorkerId = first.Id, CrewBusinessPartner = fixture.FirstCrew, Amount = 3_000m, PayeeName = "张三", RecipientNameSnapshot = "张三", TradeSnapshot = "钢筋工", CrewNameSnapshot = fixture.FirstCrew.Name });
        batch.Payments.Add(new PayrollPayment { Batch = batch, RecipientType = PayrollRecipientType.CrewWorker, RecipientKey = $"crew:{second.Id:N}", ConstructionWorkerId = second.Id, CrewBusinessPartner = fixture.FirstCrew, Amount = 4_000m, PayeeName = "李四", RecipientNameSnapshot = "李四", TradeSnapshot = "钢筋工", CrewNameSnapshot = fixture.FirstCrew.Name });
        fixture.Db.Add(batch);
        await fixture.Db.SaveChangesAsync();

        var details = await fixture.Service.GetAsync(fixture.FirstCrew.Id, false, CancellationToken.None);

        var paymentBatch = details!.PaymentBatches.Should().ContainSingle().Subject;
        paymentBatch.Amount.Should().Be(7_000m);
        paymentBatch.Lines.Should().BeEquivalentTo(
            [
                new { ConstructionWorkerId = first.Id, RecipientName = "张三", Amount = 3_000m },
                new { ConstructionWorkerId = second.Id, RecipientName = "李四", Amount = 4_000m }
            ],
            options => options.IncludingAllDeclaredProperties());
    }

    [Fact]
    public async Task CrewSearchUsesCrewContactAndWorkerFields()
    {
        await using var fixture = await CrewFixture.CreateAsync();
        await fixture.Service.AddWorkerAsync("admin", new CreateConstructionWorkerRequest(fixture.FirstCrew.Id, "搜索工人", null, "13700000000", null, null, "木工", new DateOnly(2026, 7, 1), "工人备注", "建立名册"), CancellationToken.None);

        (await fixture.Service.ListAsync(false, "搜索工人 木工 工人备注", CancellationToken.None)).Should().ContainSingle(item => item.Id == fixture.FirstCrew.Id);
        (await fixture.Service.ListAsync(false, "搜索工人 不存在", CancellationToken.None)).Should().BeEmpty();
    }

    private sealed class CrewFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private CrewFixture(SqliteConnection connection, ApplicationDbContext db, IConstructionCrewService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IConstructionCrewService Service { get; }
        public BusinessPartner FirstCrew { get; private set; } = null!;
        public BusinessPartner SecondCrew { get; private set; } = null!;
        public BusinessPartner Supplier { get; private set; } = null!;

        public static async Task<CrewFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new CrewFixture(connection, db, new ConstructionCrewService(db));
            fixture.FirstCrew = CreatePartner("CREW-01", "钢筋班组", BusinessPartnerRoleType.ConstructionCrew);
            fixture.SecondCrew = CreatePartner("CREW-02", "木工班组", BusinessPartnerRoleType.ConstructionCrew);
            fixture.Supplier = CreatePartner("SUP-01", "材料供应商", BusinessPartnerRoleType.MaterialSupplier);
            db.AddRange(fixture.FirstCrew, fixture.SecondCrew, fixture.Supplier);
            await db.SaveChangesAsync();
            return fixture;
        }

        private static BusinessPartner CreatePartner(string number, string name, BusinessPartnerRoleType role)
        {
            var partner = new BusinessPartner { PartnerNumber = number, Name = name, ShortName = name };
            partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = role });
            return partner;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
