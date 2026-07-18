using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class PayrollDisbursementModelTests
{
    [Fact]
    public async Task UnifiedBatchPersistsEmployeesCrewWorkersTemporaryWorkersAndCrewAllocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var company = new LegalEntity { Code = "PAY-UNIFIED-LE", Name = "统一发放公司", ShortName = "发放公司" };
        var project = new Project { ProjectNumber = "PAY-UNIFIED-P", Name = "统一发放项目", Stage = ProjectStage.UnderConstruction };
        var account = new FinancialAccount { LegalEntity = company, AccountName = "统一发放账户", AccountType = FinancialAccountType.Bank };
        var employee = new Employee { EmployeeNumber = "PAY-UNIFIED-E", Name = "内部员工", EmployeeType = EmployeeType.Formal };
        var crew = new BusinessPartner { PartnerNumber = "PAY-UNIFIED-C", Name = "钢筋施工班组", ShortName = "钢筋班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var worker = new ConstructionWorker { Name = "班组工人", Trade = "钢筋工" };
        worker.Memberships.Add(new ConstructionCrewMembership { Worker = worker, CrewBusinessPartner = crew, StartDate = new DateOnly(2026, 7, 1), IsPrimary = true });
        var temporary = new TemporaryWorker { Name = "临时工人", DefaultProject = project };
        var batch = new PayrollBatch
        {
            BatchNumber = "PAY-UNIFIED-B",
            Name = "统一发放测试",
            BatchType = PayrollBatchType.Temporary,
            StartDate = new DateOnly(2026, 7, 18),
            EndDate = new DateOnly(2026, 7, 18),
            PaymentDate = new DateOnly(2026, 7, 18),
            Project = project,
            LegalEntity = company,
            Account = account,
            ActualAmount = 10_000m,
            PaymentMethod = PaymentMethod.BankTransfer,
            IsUnifiedDisbursement = true,
            Status = PayrollBatchStatus.Confirmed
        };
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.Employee,
            RecipientKey = $"employee:{employee.Id:N}",
            Employee = employee,
            Amount = 3_000m,
            RecipientNameSnapshot = employee.Name,
            PayeeType = PayrollPayeeType.Employee,
            PayeeName = employee.Name
        });
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.CrewWorker,
            RecipientKey = $"crew-worker:{worker.Id:N}",
            ConstructionWorker = worker,
            CrewBusinessPartner = crew,
            Amount = 4_000m,
            RecipientNameSnapshot = worker.Name,
            CrewNameSnapshot = crew.Name,
            PayeeType = PayrollPayeeType.CrewLeader,
            PayeeName = worker.Name
        });
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.TemporaryWorker,
            RecipientKey = $"temporary-worker:{temporary.Id:N}",
            TemporaryWorker = temporary,
            Amount = 3_000m,
            RecipientNameSnapshot = temporary.Name,
            PayeeType = PayrollPayeeType.EntrustedRecipient,
            PayeeName = temporary.Name
        });
        batch.CrewAllocations.Add(new PayrollCrewAllocation { Batch = batch, CrewBusinessPartner = crew, Notes = "工程款待关联" });

        db.AddRange(company, project, employee, crew, worker, temporary, account, batch);
        await db.SaveChangesAsync();

        (await db.PayrollPayments.CountAsync()).Should().Be(3);
        (await db.ConstructionCrewMemberships.SingleAsync()).CrewBusinessPartnerId.Should().Be(crew.Id);
        (await db.PayrollCrewAllocations.SingleAsync()).Should().Match<PayrollCrewAllocation>(item =>
            item.CrewBusinessPartnerId == crew.Id && item.PayrollBatchId == batch.Id);
        (await db.PayrollBatches.SingleAsync()).ActualAmount.Should().Be(10_000m);
    }

    [Fact]
    public async Task RecipientKeyIsUniqueInsideOneBatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var employee = new Employee { EmployeeNumber = "PAY-DUP-E", Name = "重复员工", EmployeeType = EmployeeType.Formal };
        var batch = new PayrollBatch
        {
            BatchNumber = "PAY-DUP-B",
            Name = "重复校验",
            BatchType = PayrollBatchType.Temporary,
            StartDate = new DateOnly(2026, 7, 18),
            EndDate = new DateOnly(2026, 7, 18)
        };
        var key = $"employee:{employee.Id:N}";
        batch.Payments.Add(new PayrollPayment { Batch = batch, Employee = employee, RecipientType = PayrollRecipientType.Employee, RecipientKey = key, Amount = 100m, PayeeName = employee.Name, RecipientNameSnapshot = employee.Name });
        batch.Payments.Add(new PayrollPayment { Batch = batch, Employee = employee, RecipientType = PayrollRecipientType.Employee, RecipientKey = key, Amount = 200m, PayeeName = employee.Name, RecipientNameSnapshot = employee.Name });
        db.AddRange(employee, batch);

        var action = () => db.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    private static ApplicationDbContext CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
}
