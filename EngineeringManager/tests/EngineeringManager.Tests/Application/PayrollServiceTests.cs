using EngineeringManager.Application.Payroll;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Payroll;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class PayrollServiceTests
{
    [Fact]
    public async Task MixedPayrollItemsCalculateEmployeePayableAmount()
    {
        await using var fixture = await PayrollFixture.CreateAsync();
        var batch = await fixture.Service.CreateBatchAsync(
            new CreatePayrollBatchRequest("PAY-SVC-01", "阶段工资", PayrollBatchType.DateRange, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20), null, fixture.LegalEntity.Id, null),
            CancellationToken.None);
        await fixture.Service.AddItemAsync(
            new CreatePayrollItemRequest(batch.Id, fixture.Employee.Id, PayrollItemType.FixedSalary, PayrollItemNature.Earning, null, null, 5000m, "基本工资", []),
            CancellationToken.None);
        await fixture.Service.AddItemAsync(
            new CreatePayrollItemRequest(batch.Id, fixture.Employee.Id, PayrollItemType.DailyWage, PayrollItemNature.Earning, 10m, 300m, null, "计日工资", [new PayrollCostAllocationRequest(fixture.FirstProject.Id, fixture.LegalEntity.Id, 2000m), new PayrollCostAllocationRequest(fixture.SecondProject.Id, fixture.LegalEntity.Id, 1000m)]),
            CancellationToken.None);
        await fixture.Service.AddItemAsync(
            new CreatePayrollItemRequest(batch.Id, fixture.Employee.Id, PayrollItemType.Penalty, PayrollItemNature.Deduction, null, null, 500m, "扣款", []),
            CancellationToken.None);

        var summary = await fixture.Service.GetBatchSummaryAsync(batch.Id, CancellationToken.None);
        var overview = await fixture.Service.GetOverviewAsync(CancellationToken.None);

        summary.GrossEarnings.Should().Be(8000m);
        summary.DeductionAmount.Should().Be(500m);
        summary.PayableAmount.Should().Be(7500m);
        summary.EmployeeSummaries.Should().ContainSingle(item => item.EmployeeId == fixture.Employee.Id && item.PayableAmount == 7500m);
        overview.PayableAmount.Should().Be(7500m);
        overview.Batches.Should().ContainSingle(item => item.Batch.Id == batch.Id && item.Summary.PayableAmount == 7500m);
    }

    [Fact]
    public async Task CostAllocationMustEqualCalculatedPayrollItemAmount()
    {
        await using var fixture = await PayrollFixture.CreateAsync();
        var batch = await fixture.Service.CreateBatchAsync(
            new CreatePayrollBatchRequest("PAY-SVC-02", "分摊工资", PayrollBatchType.Temporary, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), fixture.FirstProject.Id, fixture.LegalEntity.Id, null),
            CancellationToken.None);
        var request = new CreatePayrollItemRequest(
            batch.Id,
            fixture.Employee.Id,
            PayrollItemType.Piecework,
            PayrollItemNature.Earning,
            100m,
            25m,
            null,
            "计件",
            [new PayrollCostAllocationRequest(fixture.FirstProject.Id, fixture.LegalEntity.Id, 2000m)]);

        var action = () => fixture.Service.AddItemAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*分摊*");
        (await fixture.Db.PayrollItems.CountAsync()).Should().Be(0);
    }

    private sealed class PayrollFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private PayrollFixture(SqliteConnection connection, ApplicationDbContext db, IPayrollService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IPayrollService Service { get; }
        public LegalEntity LegalEntity { get; private set; } = null!;
        public Employee Employee { get; private set; } = null!;
        public Project FirstProject { get; private set; } = null!;
        public Project SecondProject { get; private set; } = null!;

        public static async Task<PayrollFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new PayrollFixture(connection, db, new PayrollService(db));
            fixture.LegalEntity = new LegalEntity { Code = "PAY-SVC-LE", Name = "工资服务公司", ShortName = "服务公司" };
            fixture.Employee = new Employee { EmployeeNumber = "PAY-SVC-E", Name = "工资服务员工", EmployeeType = EmployeeType.Formal };
            fixture.FirstProject = new Project { ProjectNumber = "PAY-SVC-P1", Name = "工资项目一", Stage = ProjectStage.UnderConstruction };
            fixture.SecondProject = new Project { ProjectNumber = "PAY-SVC-P2", Name = "工资项目二", Stage = ProjectStage.UnderConstruction };
            fixture.FirstProject.LegalEntities.Add(new ProjectLegalEntity { Project = fixture.FirstProject, LegalEntity = fixture.LegalEntity, IsPrimary = true });
            fixture.SecondProject.LegalEntities.Add(new ProjectLegalEntity { Project = fixture.SecondProject, LegalEntity = fixture.LegalEntity, IsPrimary = true });
            db.AddRange(fixture.LegalEntity, fixture.Employee, fixture.FirstProject, fixture.SecondProject);
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
