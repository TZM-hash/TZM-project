using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Payroll;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class PayrollDisbursementServiceTests
{
    [Fact]
    public async Task MixedReviewedBatchCreatesOneAccountOutflowAndCategoryTotals()
    {
        await using var fixture = await PayrollDisbursementFixture.CreateAsync();

        var saved = await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.CreateRequest(10_000m, PayrollBatchStatus.Confirmed),
            CancellationToken.None);

        saved.Summary.EmployeeAmount.Should().Be(6_000m);
        saved.Summary.CrewAmount.Should().Be(4_000m);
        saved.Summary.Difference.Should().Be(0m);
        saved.Lines.Should().HaveCount(3);
        saved.Lines.Should().Contain(item => item.RecipientType == PayrollRecipientType.Employee && item.EmployeeId == fixture.SpecialTemporaryEmployee.Id);
        saved.Lines.Should().Contain(item => item.RecipientType == PayrollRecipientType.CrewWorker && item.CrewBusinessPartnerId == fixture.Crew.Id && item.CrewNameSnapshot == fixture.Crew.Name);
        var overview = await fixture.Service.GetDisbursementOverviewAsync(CancellationToken.None);
        overview.ActualAmount.Should().Be(10_000m);
        overview.EmployeeAmount.Should().Be(6_000m);
        overview.Batches.Should().ContainSingle(item => item.Batch.Id == saved.Batch.Id);
        var specialTemporaryPayment = await fixture.Db.PayrollPayments.SingleAsync(item => item.EmployeeId == fixture.SpecialTemporaryEmployee.Id);
        specialTemporaryPayment.Should().Match<PayrollPayment>(item =>
            item.RecipientType == PayrollRecipientType.Employee &&
            item.RecipientKey == $"employee:{fixture.SpecialTemporaryEmployee.Id:N}");
        var transactions = await fixture.Db.AccountTransactions.Where(item => item.SourceType == AccountTransactionSourceType.PayrollPayment).ToListAsync();
        transactions.Should().ContainSingle().Which.Should().Match<AccountTransaction>(item => item.SourceId == saved.Batch.Id && item.Amount == 10_000m && item.AccountId == fixture.Account.Id);
        (await fixture.Db.PayrollCrewAllocations.SingleAsync()).Should().Match<PayrollCrewAllocation>(item => item.CrewBusinessPartnerId == fixture.Crew.Id && item.PayableEntryId == null);
    }

    [Fact]
    public async Task UpdatingReviewedBatchReusesAccountTransactionAndWritesAudit()
    {
        await using var fixture = await PayrollDisbursementFixture.CreateAsync();
        var created = await fixture.Service.SaveDisbursementBatchAsync("admin", fixture.CreateRequest(10_000m, PayrollBatchStatus.Confirmed), CancellationToken.None);
        var updatedLines = created.Lines.Select(item => item.EmployeeId == fixture.Employee.Id
            ? new PayrollDisbursementLineRequest(item.Id, item.RecipientType, item.EmployeeId, item.ConstructionWorkerId, item.CrewBusinessPartnerId, 4_000m, item.Notes)
            : new PayrollDisbursementLineRequest(item.Id, item.RecipientType, item.EmployeeId, item.ConstructionWorkerId, item.CrewBusinessPartnerId, item.Amount, item.Notes)).ToArray();

        var updated = await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.CreateRequest(11_000m, PayrollBatchStatus.Confirmed) with
            {
                Id = created.Batch.Id,
                ConcurrencyStamp = created.Batch.ConcurrencyStamp,
                Lines = updatedLines,
                Reason = "修正员工实际发放金额"
            },
            CancellationToken.None);

        updated.Summary.DetailAmount.Should().Be(11_000m);
        (await fixture.Db.AccountTransactions.CountAsync(item => item.SourceType == AccountTransactionSourceType.PayrollPayment)).Should().Be(1);
        (await fixture.Db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.PayrollPayment)).Amount.Should().Be(11_000m);
        (await fixture.Db.AuditLogs.CountAsync(item => item.EntityType == nameof(PayrollBatch))).Should().Be(2);
    }

    [Fact]
    public async Task ExistingInactiveEmployeeAndEndedCrewMembershipCanBeSavedWithoutDroppingPayments()
    {
        await using var fixture = await PayrollDisbursementFixture.CreateAsync();
        var created = await fixture.Service.SaveDisbursementBatchAsync("admin", fixture.CreateRequest(10_000m, PayrollBatchStatus.Confirmed), CancellationToken.None);
        fixture.Employee.IsActive = false;
        fixture.CrewWorker.Memberships.Single().EndDate = new DateOnly(2026, 7, 17);
        await fixture.Db.SaveChangesAsync();
        var existingLines = created.Lines.Select(item => new PayrollDisbursementLineRequest(
            item.Id,
            item.RecipientType,
            item.EmployeeId,
            item.ConstructionWorkerId,
            item.CrewBusinessPartnerId,
            item.Amount,
            item.Notes)).ToArray();

        var updated = await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.CreateRequest(10_000m, PayrollBatchStatus.Confirmed) with
            {
                Id = created.Batch.Id,
                ConcurrencyStamp = created.Batch.ConcurrencyStamp,
                Lines = existingLines,
                Reason = "保存历史人员工资"
            },
            CancellationToken.None);

        updated.Lines.Should().HaveCount(3);
        updated.Lines.Should().Contain(item => item.EmployeeId == fixture.Employee.Id);
        updated.Lines.Should().Contain(item => item.ConstructionWorkerId == fixture.CrewWorker.Id && item.CrewBusinessPartnerId == fixture.Crew.Id);
    }

    [Fact]
    public async Task NewInactiveEmployeeStillCannotBeAddedToExistingBatch()
    {
        await using var fixture = await PayrollDisbursementFixture.CreateAsync();
        var created = await fixture.Service.SaveDisbursementBatchAsync("admin", fixture.CreateRequest(10_000m, PayrollBatchStatus.Confirmed), CancellationToken.None);
        var inactiveEmployee = new Employee { EmployeeNumber = "PAY-INACTIVE-NEW", Name = "新增停用员工", IsActive = false };
        fixture.Db.Employees.Add(inactiveEmployee);
        await fixture.Db.SaveChangesAsync();
        var lines = created.Lines.Select(item => new PayrollDisbursementLineRequest(
            item.Id,
            item.RecipientType,
            item.EmployeeId,
            item.ConstructionWorkerId,
            item.CrewBusinessPartnerId,
            item.Amount,
            item.Notes)).Append(new PayrollDisbursementLineRequest(null, PayrollRecipientType.Employee, inactiveEmployee.Id, null, null, 1_000m, null)).ToArray();

        var action = () => fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.CreateRequest(11_000m, PayrollBatchStatus.Confirmed) with
            {
                Id = created.Batch.Id,
                ConcurrencyStamp = created.Batch.ConcurrencyStamp,
                Lines = lines,
                Reason = "尝试新增停用员工"
            },
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*员工不存在或已停用*");
    }

    [Fact]
    public async Task NewCrewWorkerWithoutMembershipOnPaymentDateStillCannotBeAddedToExistingBatch()
    {
        await using var fixture = await PayrollDisbursementFixture.CreateAsync();
        var created = await fixture.Service.SaveDisbursementBatchAsync("admin", fixture.CreateRequest(10_000m, PayrollBatchStatus.Confirmed), CancellationToken.None);
        var unrelatedWorker = new ConstructionWorker { Name = "未入组人员", IsActive = true };
        fixture.Db.ConstructionWorkers.Add(unrelatedWorker);
        await fixture.Db.SaveChangesAsync();
        var lines = created.Lines.Select(item => new PayrollDisbursementLineRequest(
            item.Id,
            item.RecipientType,
            item.EmployeeId,
            item.ConstructionWorkerId,
            item.CrewBusinessPartnerId,
            item.Amount,
            item.Notes)).Append(new PayrollDisbursementLineRequest(
                null,
                PayrollRecipientType.CrewWorker,
                null,
                unrelatedWorker.Id,
                fixture.Crew.Id,
                1_000m,
                null)).ToArray();

        var action = () => fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.CreateRequest(11_000m, PayrollBatchStatus.Confirmed) with
            {
                Id = created.Batch.Id,
                ConcurrencyStamp = created.Batch.ConcurrencyStamp,
                Lines = lines,
                Reason = "尝试新增未入组人员"
            },
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*发放日期不属于所选施工班组*");
    }

    [Fact]
    public async Task ReviewedBatchRejectsDifferenceAndCrewWithoutProjectAtomically()
    {
        await using var fixture = await PayrollDisbursementFixture.CreateAsync();
        var mismatch = fixture.CreateRequest(11_000m, PayrollBatchStatus.Confirmed);
        var missingProject = fixture.CreateRequest(10_000m, PayrollBatchStatus.Confirmed) with { ProjectId = null };

        var mismatchAction = () => fixture.Service.SaveDisbursementBatchAsync("admin", mismatch, CancellationToken.None);
        var projectAction = () => fixture.Service.SaveDisbursementBatchAsync("admin", missingProject, CancellationToken.None);

        await mismatchAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*差额*");
        await projectAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*项目*");
        (await fixture.Db.PayrollBatches.CountAsync()).Should().Be(0);
        (await fixture.Db.AccountTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task VoidingReviewedBatchCreatesOneReversalAndRemovesItFromEffectiveOverview()
    {
        await using var fixture = await PayrollDisbursementFixture.CreateAsync();
        var created = await fixture.Service.SaveDisbursementBatchAsync("admin", fixture.CreateRequest(10_000m, PayrollBatchStatus.Confirmed), CancellationToken.None);
        var existingLines = created.Lines.Select(item => new PayrollDisbursementLineRequest(
            item.Id,
            item.RecipientType,
            item.EmployeeId,
            item.ConstructionWorkerId,
            item.CrewBusinessPartnerId,
            item.Amount,
            item.Notes)).ToArray();

        await fixture.Service.SaveDisbursementBatchAsync(
            "admin",
            fixture.CreateRequest(10_000m, PayrollBatchStatus.Voided) with
            {
                Id = created.Batch.Id,
                ConcurrencyStamp = created.Batch.ConcurrencyStamp,
                Lines = existingLines,
                Reason = "银行退回，作废本次真实发放"
            },
            CancellationToken.None);

        var transactions = await fixture.Db.AccountTransactions.OrderBy(item => item.Direction).ToListAsync();
        transactions.Should().HaveCount(2);
        transactions.Should().ContainSingle(item => item.SourceType == AccountTransactionSourceType.PayrollPayment && item.Direction == AccountTransactionDirection.Outflow && item.Amount == 10_000m);
        transactions.Should().ContainSingle(item => item.SourceType == AccountTransactionSourceType.PayrollPaymentReversal && item.Direction == AccountTransactionDirection.Inflow && item.Amount == 10_000m && item.SourceId == created.Batch.Id);
        (await fixture.Service.GetDisbursementOverviewAsync(CancellationToken.None)).ActualAmount.Should().Be(0m);
    }

    private sealed class PayrollDisbursementFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private PayrollDisbursementFixture(SqliteConnection connection, ApplicationDbContext db, IPayrollService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IPayrollService Service { get; }
        public LegalEntity Company { get; private set; } = null!;
        public Project Project { get; private set; } = null!;
        public FinancialAccount Account { get; private set; } = null!;
        public Employee Employee { get; private set; } = null!;
        public Employee SpecialTemporaryEmployee { get; private set; } = null!;
        public BusinessPartner Crew { get; private set; } = null!;
        public ConstructionWorker CrewWorker { get; private set; } = null!;

        public SavePayrollDisbursementBatchRequest CreateRequest(decimal actualAmount, PayrollBatchStatus status) => new(
            null,
            "PAY-MIXED-001",
            "混合工资发放",
            new DateOnly(2026, 7, 18),
            Project.Id,
            Company.Id,
            Account.Id,
            actualAmount,
            PaymentMethod.BankTransfer,
            "BANK-001",
            status,
            "员工、班组和特殊临时人员混合发放",
            null,
            "登记真实发放",
            [
                new PayrollDisbursementLineRequest(null, PayrollRecipientType.Employee, Employee.Id, null, null, 3_000m, "员工工资"),
                new PayrollDisbursementLineRequest(null, PayrollRecipientType.CrewWorker, null, CrewWorker.Id, Crew.Id, 4_000m, "班组民工工资"),
                new PayrollDisbursementLineRequest(null, PayrollRecipientType.Employee, SpecialTemporaryEmployee.Id, null, null, 3_000m, "特殊临时人员工资")
            ],
            [new PayrollCrewAllocationRequest(Crew.Id, null, null, "工程款待关联")]);

        public static async Task<PayrollDisbursementFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new PayrollDisbursementFixture(connection, db, new PayrollService(db));
            fixture.Company = new LegalEntity { Code = "PAY-MIXED-LE", Name = "混合发放公司", ShortName = "发放公司" };
            fixture.Project = new Project { ProjectNumber = "PAY-MIXED-P", Name = "混合发放项目", Stage = ProjectStage.UnderConstruction };
            fixture.Project.LegalEntities.Add(new ProjectLegalEntity { Project = fixture.Project, LegalEntity = fixture.Company, IsPrimary = true });
            fixture.Account = new FinancialAccount { LegalEntity = fixture.Company, AccountName = "工资专户", AccountType = FinancialAccountType.Bank };
            fixture.Employee = new Employee { EmployeeNumber = "PAY-MIXED-E", Name = "内部员工", EmployeeType = EmployeeType.Formal };
            fixture.SpecialTemporaryEmployee = new Employee { EmployeeNumber = "PAY-MIXED-T", Name = "特殊临时人员", EmployeeType = EmployeeType.Temporary };
            fixture.Crew = new BusinessPartner { PartnerNumber = "PAY-MIXED-C", Name = "钢筋施工班组", ShortName = "钢筋班组" };
            fixture.Crew.Roles.Add(new BusinessPartnerRole { Partner = fixture.Crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
            fixture.CrewWorker = new ConstructionWorker { Name = "班组张三", IdentityNumber = "110101199001010011", Trade = "钢筋工" };
            fixture.CrewWorker.Memberships.Add(new ConstructionCrewMembership { Worker = fixture.CrewWorker, CrewBusinessPartner = fixture.Crew, StartDate = new DateOnly(2026, 7, 1), IsPrimary = true });
            db.AddRange(fixture.Company, fixture.Project, fixture.Account, fixture.Employee, fixture.SpecialTemporaryEmployee, fixture.Crew, fixture.CrewWorker);
            await db.SaveChangesAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
