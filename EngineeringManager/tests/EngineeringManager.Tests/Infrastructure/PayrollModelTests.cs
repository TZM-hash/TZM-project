using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class PayrollModelTests
{
    [Fact]
    public async Task PayrollBatchItemsAllocationsAndPaymentsCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var legalEntity = new LegalEntity { Code = "PAY-LE", Name = "工资测试公司", ShortName = "工资公司" };
        var project = new Project { ProjectNumber = "PAY-P", Name = "工资测试项目", Stage = ProjectStage.UnderConstruction };
        var employee = new Employee { EmployeeNumber = "PAY-E", Name = "工资测试员工", EmployeeType = EmployeeType.Formal };
        var account = new FinancialAccount { LegalEntity = legalEntity, AccountName = "工资账户", AccountType = EngineeringManager.Domain.Finance.FinancialAccountType.Bank };
        var batch = new PayrollBatch { BatchNumber = "PAY-B", Name = "七月工资", BatchType = PayrollBatchType.Monthly, StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 31), LegalEntity = legalEntity };
        var item = new PayrollItem { Batch = batch, Employee = employee, ItemType = PayrollItemType.FixedSalary, Nature = PayrollItemNature.Earning, Amount = 5000m };
        item.CostAllocations.Add(new PayrollCostAllocation { PayrollItem = item, Project = project, LegalEntity = legalEntity, Amount = 5000m });
        batch.Items.Add(item);
        var payment = new PayrollPayment { Batch = batch, Employee = employee, Account = account, PaymentDate = new DateOnly(2026, 8, 5), Amount = 3000m, PayeeType = PayrollPayeeType.Employee, PayeeName = employee.Name };

        db.AddRange(legalEntity, project, employee, account, batch, payment);
        await db.SaveChangesAsync();

        (await db.PayrollItems.SingleAsync()).Amount.Should().Be(5000m);
        (await db.PayrollCostAllocations.SingleAsync()).ProjectId.Should().Be(project.Id);
        (await db.PayrollPayments.SingleAsync()).Amount.Should().Be(3000m);
    }
}
