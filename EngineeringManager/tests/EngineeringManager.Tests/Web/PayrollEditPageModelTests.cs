using System.Security.Claims;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Web.Pages.Payroll;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Web;

public sealed class PayrollEditPageModelTests
{
    [Fact]
    public async Task ExistingInactiveEmployeeEndedMembershipAndAllocationLinksSurvivePageRoundTrip()
    {
        await using var fixture = await PageFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "PAGE-HIST-E", Name = "停用员工", IsActive = false };
        var crew = CreateCrew("PAGE-HIST-C", "历史班组");
        var worker = new ConstructionWorker { Name = "历史班组人员" };
        worker.Memberships.Add(new ConstructionCrewMembership
        {
            Worker = worker,
            CrewBusinessPartner = crew,
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            IsPrimary = true
        });
        fixture.Db.AddRange(employee, crew, worker);
        await fixture.Db.SaveChangesAsync();

        var employeePaymentId = Guid.NewGuid();
        var crewPaymentId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var payableEntryId = Guid.NewGuid();
        fixture.Service.Details = CreateDetails(
            [
                CreateLine(employeePaymentId, PayrollRecipientType.Employee, employee.Id, null, null, 3_000m, employee.Name),
                CreateLine(crewPaymentId, PayrollRecipientType.CrewWorker, null, worker.Id, crew.Id, 4_000m, worker.Name, crew.Name)
            ],
            [new PayrollCrewAllocationDto(Guid.NewGuid(), crew.Id, contractId, payableEntryId, "保留工程款关联", Guid.NewGuid())]);
        var model = CreateModel(fixture);

        await model.OnGetAsync(CancellationToken.None);

        model.Input.EmployeeLines.Should().ContainSingle(item => item.PaymentId == employeePaymentId && item.Selected && item.Amount == 3_000m);
        model.Input.CrewLines.Should().ContainSingle(item => item.PaymentId == crewPaymentId && item.Selected && item.Amount == 4_000m && item.CrewBusinessPartnerId == crew.Id);

        await model.OnPostAsync(CancellationToken.None);

        fixture.Service.SavedRequest.Should().NotBeNull();
        fixture.Service.SavedRequest!.Lines.Should().Contain(item => item.Id == employeePaymentId && item.EmployeeId == employee.Id);
        fixture.Service.SavedRequest.Lines.Should().Contain(item => item.Id == crewPaymentId && item.ConstructionWorkerId == worker.Id && item.CrewBusinessPartnerId == crew.Id);
        fixture.Service.SavedRequest.CrewAllocations.Should().ContainSingle().Which.Should().Be(
            new PayrollCrewAllocationRequest(crew.Id, contractId, payableEntryId, "保留工程款关联"));
    }

    [Fact]
    public async Task WorkerWithTwoMembershipsMatchesExistingPaymentOnlyToItsCrew()
    {
        await using var fixture = await PageFixture.CreateAsync();
        var firstCrew = CreateCrew("PAGE-MULTI-C1", "一班组");
        var secondCrew = CreateCrew("PAGE-MULTI-C2", "二班组");
        var worker = new ConstructionWorker { Name = "跨班组人员" };
        worker.Memberships.Add(new ConstructionCrewMembership { Worker = worker, CrewBusinessPartner = firstCrew, StartDate = new DateOnly(2026, 1, 1) });
        worker.Memberships.Add(new ConstructionCrewMembership { Worker = worker, CrewBusinessPartner = secondCrew, StartDate = new DateOnly(2026, 1, 1) });
        fixture.Db.AddRange(firstCrew, secondCrew, worker);
        await fixture.Db.SaveChangesAsync();

        var paymentId = Guid.NewGuid();
        fixture.Service.Details = CreateDetails(
            [CreateLine(paymentId, PayrollRecipientType.CrewWorker, null, worker.Id, secondCrew.Id, 4_000m, worker.Name, secondCrew.Name)],
            []);
        var model = CreateModel(fixture);

        await model.OnGetAsync(CancellationToken.None);

        model.Input.CrewLines.Should().HaveCount(2);
        model.Input.CrewLines.Should().ContainSingle(item => item.Selected).Which.Should().Match<EditModel.PersonLineInput>(item =>
            item.PaymentId == paymentId && item.CrewBusinessPartnerId == secondCrew.Id && item.Amount == 4_000m);
        model.Input.CrewLines.Single(item => item.CrewBusinessPartnerId == firstCrew.Id).PaymentId.Should().BeNull();
    }

    private static EditModel CreateModel(PageFixture fixture)
    {
        var identity = new ClaimsIdentity("PayrollEditTest", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "payroll-edit-user"));
        return new EditModel(fixture.Service, fixture.Db)
        {
            Id = fixture.Service.Details.Batch.Id,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static BusinessPartner CreateCrew(string number, string name)
    {
        var crew = new BusinessPartner { PartnerNumber = number, Name = name, ShortName = name };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        return crew;
    }

    private static PayrollDisbursementLineDto CreateLine(
        Guid id,
        PayrollRecipientType type,
        Guid? employeeId,
        Guid? workerId,
        Guid? crewId,
        decimal amount,
        string name,
        string? crewName = null) =>
        new(id, type, employeeId, workerId, crewId, amount, name, null, null, null, null, crewName, null, Guid.NewGuid());

    private static PayrollDisbursementBatchDetailsDto CreateDetails(
        IReadOnlyList<PayrollDisbursementLineDto> lines,
        IReadOnlyList<PayrollCrewAllocationDto> allocations)
    {
        var batchId = Guid.NewGuid();
        var actualAmount = lines.Sum(item => item.Amount);
        var summary = PayrollDisbursementRules.Calculate(
            actualAmount,
            lines.Select(item => new PayrollDisbursementLineInput(item.RecipientType, item.EmployeeId, item.ConstructionWorkerId, item.CrewBusinessPartnerId, item.Amount)));
        return new PayrollDisbursementBatchDetailsDto(
            new PayrollDisbursementBatchDto(batchId, "PAGE-001", "页面往返", new DateOnly(2026, 7, 18), null, null, null, actualAmount, PaymentMethod.BankTransfer, null, PayrollBatchStatus.Draft, null, true, Guid.NewGuid()),
            summary,
            lines,
            allocations);
    }

    private sealed class RecordingPayrollService : IPayrollService
    {
        public PayrollDisbursementBatchDetailsDto Details { get; set; } = null!;
        public SavePayrollDisbursementBatchRequest? SavedRequest { get; private set; }

        public Task<PayrollDisbursementBatchDetailsDto> SaveDisbursementBatchAsync(string userId, SavePayrollDisbursementBatchRequest request, CancellationToken cancellationToken)
        {
            SavedRequest = request;
            return Task.FromResult(Details);
        }

        public Task<PayrollDisbursementBatchDetailsDto?> GetDisbursementBatchAsync(Guid batchId, CancellationToken cancellationToken) =>
            Task.FromResult<PayrollDisbursementBatchDetailsDto?>(Details);

        public Task<PayrollDisbursementOverviewDto> GetDisbursementOverviewAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PayrollBatchDto> CreateBatchAsync(CreatePayrollBatchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PayrollItemDto> AddItemAsync(CreatePayrollItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordPaymentAsync(RecordPayrollPaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PayrollBatchSummaryDto> GetBatchSummaryAsync(Guid batchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PayrollBatchDto>> ListBatchesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PayrollOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PageFixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public RecordingPayrollService Service { get; } = new();

        public static async Task<PageFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new PageFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
