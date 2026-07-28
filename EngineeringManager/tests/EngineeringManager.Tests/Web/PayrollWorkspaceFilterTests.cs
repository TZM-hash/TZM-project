using System.Security.Claims;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Web.Pages.Payroll;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Web;

public sealed class PayrollWorkspaceFilterTests
{
    [Fact]
    public async Task CompanyAndPersonalScopesFilterListAndSummaryFromTheSameBatchSet()
    {
        await using var fixture = await PageFixture.CreateAsync();
        var firstCompany = new LegalEntity { Code = "PAY-SCOPE-A", Name = "甲工资发放公司", ShortName = "甲公司" };
        var secondCompany = new LegalEntity { Code = "PAY-SCOPE-B", Name = "乙工资发放公司", ShortName = "乙公司" };
        fixture.Db.LegalEntities.AddRange(firstCompany, secondCompany);
        await fixture.Db.SaveChangesAsync();

        var firstCompanyConfirmed = CreateItem(firstCompany.Id, PayrollFundingSource.CompanyAccount, PayrollBatchStatus.Confirmed, 130m, 90m, 40m);
        var personalDraft = CreateItem(firstCompany.Id, PayrollFundingSource.PersonalAdvance, PayrollBatchStatus.Draft, 50m, 50m, 0m);
        var secondCompanyConfirmed = CreateItem(secondCompany.Id, PayrollFundingSource.CompanyAccount, PayrollBatchStatus.Confirmed, 200m, 100m, 100m);
        fixture.Service.Overview = CreateOverview(firstCompanyConfirmed, personalDraft, secondCompanyConfirmed);
        var employee = new Employee { EmployeeNumber = "PAY-LIST-E", Name = "名单员工", EmployeeType = EmployeeType.Formal };
        var temporaryEmployee = new Employee
        {
            EmployeeNumber = "PAY-LIST-T",
            Name = "名单临时人员",
            EmployeeType = EmployeeType.Temporary,
            Phone = "13800000000",
            PositionTitle = "临时辅助工",
            IdentityNumber = "330100199001010011",
            BankAccountNumber = "6222020200000000011",
            BankName = "测试银行临时人员支行"
        };
        var crew = new BusinessPartner { PartnerNumber = "PAY-LIST-C", Name = "名单班组", ShortName = "名单班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var worker = new ConstructionWorker
        {
            Name = "名单班组人员",
            IdentityNumber = "330100199002020022",
            BankAccountNumber = "6222020200000000022",
            BankName = "测试银行班组支行"
        };
        var batch = new PayrollBatch
        {
            Id = firstCompanyConfirmed.Batch.Id,
            BatchNumber = firstCompanyConfirmed.Batch.BatchNumber,
            Name = firstCompanyConfirmed.Batch.Name,
            BatchType = PayrollBatchType.Monthly,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 31),
            LegalEntity = firstCompany,
            ActualAmount = 130m,
            IsUnifiedDisbursement = true,
            Status = PayrollBatchStatus.Confirmed
        };
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.Employee,
            Employee = employee,
            RecipientNameSnapshot = employee.Name,
            PayeeName = employee.Name,
            Amount = 60m
        });
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.Employee,
            Employee = temporaryEmployee,
            RecipientNameSnapshot = temporaryEmployee.Name,
            PayeeName = temporaryEmployee.Name,
            Amount = 30m
        });
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.CrewWorker,
            ConstructionWorker = worker,
            CrewBusinessPartner = crew,
            RecipientNameSnapshot = worker.Name,
            CrewNameSnapshot = crew.Name,
            PayeeName = worker.Name,
            Amount = 40m
        });
        fixture.Db.AddRange(employee, temporaryEmployee, crew, worker, batch);
        await fixture.Db.SaveChangesAsync();

        var companyModel = CreateModel(fixture, $"company:{firstCompany.Id}", PayrollBatchStatus.Confirmed, canViewSensitive: true);
        await companyModel.OnGetAsync(CancellationToken.None);

        companyModel.Batches.Should().ContainSingle().Which.Should().Be(firstCompanyConfirmed);
        companyModel.Overview.Batches.Should().Equal(companyModel.Batches);
        companyModel.Overview.ActualAmount.Should().Be(130m);
        companyModel.Overview.EmployeeAmount.Should().Be(90m);
        companyModel.Overview.CrewAmount.Should().Be(40m);
        var breakdown = companyModel.RecipientBreakdowns[firstCompanyConfirmed.Batch.Id];
        breakdown.TotalCount.Should().Be(3);
        breakdown.EmployeeCount.Should().Be(1);
        breakdown.TemporaryCount.Should().Be(1);
        breakdown.CrewCount.Should().Be(1);
        breakdown.EmployeeAmount.Should().Be(60m);
        breakdown.TemporaryAmount.Should().Be(30m);
        breakdown.CrewAmount.Should().Be(40m);
        breakdown.Employees.Should().ContainSingle(item => item.Name == employee.Name && item.Amount == 60m);
        breakdown.TemporaryWorkers.Should().ContainSingle(item =>
            item.Name == temporaryEmployee.Name
            && item.PersonNumber == temporaryEmployee.EmployeeNumber
            && item.Phone == temporaryEmployee.Phone
            && item.RoleName == temporaryEmployee.PositionTitle
            && item.IdentityNumber == temporaryEmployee.IdentityNumber
            && item.BankAccountNumber == temporaryEmployee.BankAccountNumber
            && item.BankName == temporaryEmployee.BankName
            && item.Amount == 30m);
        breakdown.CrewWorkers.Should().ContainSingle(item => item.Name == worker.Name && item.GroupName == crew.Name && item.Amount == 40m);
        breakdown.CrewWorkers.Should().ContainSingle(item =>
            item.IdentityNumber == worker.IdentityNumber
            && item.BankAccountNumber == worker.BankAccountNumber
            && item.BankName == worker.BankName);
        companyModel.DisbursementScopeOptions.Should().Contain(item =>
            item.Value == $"company:{firstCompany.Id}" && item.Label == firstCompany.ShortName);
        companyModel.DisbursementScopeOptions.Should().Contain(item =>
            item.Value == IndexModel.PersonalDisbursementScope && item.Label.Contains("私人转账"));

        var personalModel = CreateModel(fixture, IndexModel.PersonalDisbursementScope, null);
        await personalModel.OnGetAsync(CancellationToken.None);

        personalModel.Batches.Should().ContainSingle().Which.Should().Be(personalDraft);
        personalModel.Overview.Batches.Should().Equal(personalModel.Batches);
        personalModel.Overview.ActualAmount.Should().Be(50m);
        personalModel.Overview.EmployeeAmount.Should().Be(50m);
        personalModel.Overview.CrewAmount.Should().Be(0m);
        personalModel.RecipientBreakdowns.Should().BeEmpty();

        var financeModel = CreateModel(fixture, $"company:{firstCompany.Id}", PayrollBatchStatus.Confirmed);
        await financeModel.OnGetAsync(CancellationToken.None);
        financeModel.RecipientBreakdowns[firstCompanyConfirmed.Batch.Id].TemporaryWorkers.Should().OnlyContain(item =>
            item.IdentityNumber == null && item.BankAccountNumber == null && item.BankName == null);
    }

    private static IndexModel CreateModel(PageFixture fixture, string? scope, PayrollBatchStatus? status, bool canViewSensitive = false)
    {
        var identity = new ClaimsIdentity("PayrollWorkspaceTest", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "payroll-workspace-user"));
        identity.AddClaim(new Claim(ClaimTypes.Role, canViewSensitive ? SystemRoles.SystemAdministrator : SystemRoles.Finance));
        return new IndexModel(fixture.Service, fixture.Db)
        {
            DisbursementScope = scope,
            Status = status,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static PayrollDisbursementBatchListItemDto CreateItem(
        Guid companyId,
        PayrollFundingSource fundingSource,
        PayrollBatchStatus status,
        decimal actualAmount,
        decimal employeeAmount,
        decimal crewAmount)
    {
        var summary = new PayrollDisbursementSummary(employeeAmount, crewAmount, employeeAmount + crewAmount, actualAmount, actualAmount - employeeAmount - crewAmount, []);
        var batch = new PayrollDisbursementBatchDto(
            Guid.NewGuid(),
            "PAY-" + Guid.NewGuid().ToString("N")[..8],
            "筛选测试批次",
            new DateOnly(2026, 7, 28),
            null,
            companyId,
            null,
            actualAmount,
            PaymentMethod.BankTransfer,
            null,
            status,
            null,
            true,
            Guid.NewGuid(),
            FundingSource: fundingSource);
        return new PayrollDisbursementBatchListItemDto(batch, summary, 1);
    }

    private static PayrollDisbursementOverviewDto CreateOverview(params PayrollDisbursementBatchListItemDto[] batches) =>
        new(
            batches.Sum(item => item.Batch.ActualAmount),
            batches.Sum(item => item.Summary.EmployeeAmount),
            batches.Sum(item => item.Summary.CrewAmount),
            batches.Sum(item => item.Summary.Difference),
            batches);

    private sealed class StubPayrollService : IPayrollService
    {
        public PayrollDisbursementOverviewDto Overview { get; set; } = new(0m, 0m, 0m, 0m, []);

        public Task<PayrollDisbursementOverviewDto> GetDisbursementOverviewAsync(CancellationToken cancellationToken) => Task.FromResult(Overview);
        public Task<PayrollDisbursementBatchDetailsDto> SaveDisbursementBatchAsync(string userId, SavePayrollDisbursementBatchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PayrollDisbursementBatchDetailsDto?> GetDisbursementBatchAsync(Guid batchId, CancellationToken cancellationToken) => throw new NotSupportedException();
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
        public StubPayrollService Service { get; } = new();

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
