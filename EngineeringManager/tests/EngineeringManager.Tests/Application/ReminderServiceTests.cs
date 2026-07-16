using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Application.Reminders;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using EngineeringManager.Infrastructure.Payroll;
using EngineeringManager.Infrastructure.Reminders;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ReminderServiceTests
{
    [Fact]
    public async Task RefreshCreatesBusinessAndFailedTaskRemindersWithoutDuplicates()
    {
        await using var fixture = await ReminderFixture.CreateAsync();

        await fixture.Service.RefreshAsync(new DateOnly(2026, 7, 16), CancellationToken.None);
        await fixture.Service.RefreshAsync(new DateOnly(2026, 7, 16), CancellationToken.None);
        var reminders = await fixture.Service.ListAsync(includeResolved: false, CancellationToken.None);

        reminders.Should().Contain(item => item.Type == ReminderType.ProjectMilestone);
        reminders.Should().Contain(item => item.Type == ReminderType.UncollectedReceivable);
        reminders.Should().Contain(item => item.Type == ReminderType.UnpaidPayable);
        reminders.Should().Contain(item => item.Type == ReminderType.UninvoicedReceivable);
        reminders.Should().Contain(item => item.Type == ReminderType.UnpaidPayroll);
        reminders.Should().Contain(item => item.Type == ReminderType.ImportFailed);
        reminders.Should().Contain(item => item.Type == ReminderType.BackupFailed);
        reminders.Select(item => item.DeduplicationKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ReminderCanBeMarkedReadAndResolved()
    {
        await using var fixture = await ReminderFixture.CreateAsync();
        await fixture.Service.RefreshAsync(new DateOnly(2026, 7, 16), CancellationToken.None);
        var reminder = (await fixture.Service.ListAsync(false, CancellationToken.None))[0];

        await fixture.Service.MarkReadAsync(reminder.Id, CancellationToken.None);
        await fixture.Service.ResolveAsync(reminder.Id, CancellationToken.None);

        (await fixture.Service.ListAsync(false, CancellationToken.None)).Should().NotContain(item => item.Id == reminder.Id);
        (await fixture.Service.ListAsync(true, CancellationToken.None)).Single(item => item.Id == reminder.Id).Status.Should().Be(ReminderStatus.Resolved);
    }

    private sealed class ReminderFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ReminderFixture(SqliteConnection connection, ApplicationDbContext db, IReminderService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IReminderService Service { get; }

        public static async Task<ReminderFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var finance = new FinanceLedgerService(db);
            var payroll = new PayrollService(db);
            var service = new ReminderService(db, finance, payroll);
            var legalEntity = new LegalEntity { Code = "REM-LE", Name = "提醒测试公司", ShortName = "提醒公司" };
            var partner = new BusinessPartner { PartnerNumber = "REM-BP", Name = "提醒合作单位", ShortName = "提醒单位" };
            var project = new Project { ProjectNumber = "REM-P", Name = "提醒测试项目", Stage = ProjectStage.UnderConstruction };
            project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = legalEntity, IsPrimary = true });
            project.Milestones.Add(new ProjectMilestone { Project = project, Name = "节点一", PlannedDate = new DateOnly(2026, 7, 15), IsCompleted = false });
            var employee = new Employee { EmployeeNumber = "REM-E", Name = "提醒员工", EmployeeType = EmployeeType.Formal };
            db.AddRange(legalEntity, partner, project, employee);
            await db.SaveChangesAsync();
            await finance.AddReceivableAsync(new CreateReceivableRequest(project.Id, null, legalEntity.Id, partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 100m, null), CancellationToken.None);
            await finance.AddPayableAsync(new CreatePayableRequest(project.Id, null, legalEntity.Id, partner.Id, PayableSourceType.Manual, new DateOnly(2026, 7, 1), null, 80m, null), CancellationToken.None);
            var batch = await payroll.CreateBatchAsync(new CreatePayrollBatchRequest("REM-PAY", "提醒工资", PayrollBatchType.Monthly, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), project.Id, legalEntity.Id, null), CancellationToken.None);
            await payroll.AddItemAsync(new CreatePayrollItemRequest(batch.Id, employee.Id, PayrollItemType.FixedSalary, PayrollItemNature.Earning, null, null, 5000m, null, []), CancellationToken.None);
            db.ImportBatches.Add(new ImportBatch { CreatedByUserId = "user", Dataset = ExportDataset.Employees, OriginalFileName = "bad.xlsx", OriginalContent = [1], Status = DataExchangeTaskStatus.Failed });
            db.BackupTasks.Add(new BackupTask { RequestedByUserId = "admin", Status = DataExchangeTaskStatus.Failed, ErrorMessage = "备份失败" });
            await db.SaveChangesAsync();
            return new ReminderFixture(connection, db, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
