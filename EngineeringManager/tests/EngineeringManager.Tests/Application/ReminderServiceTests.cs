using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Application.Reminders;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Offline;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Domain.StageResults;
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
        reminders.Should().Contain(item => item.Type == ReminderType.CompanyCertificateExpiring);
        reminders.Should().Contain(item => item.Type == ReminderType.EmployeeCertificateExpiring);
        reminders.Should().Contain(item => item.Type == ReminderType.EquipmentLeaseExpiring);
        reminders.Should().Contain(item => item.Type == ReminderType.EquipmentMaintenanceDue);
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

    [Fact]
    public async Task OfflineFailureCreatesReminderAndSuccessfulSyncResolvesIt()
    {
        await using var fixture = await ReminderFixture.CreateAsync();
        var project = await fixture.Db.Projects.SingleAsync();
        var user = new ApplicationUser { UserName = "offline-reminder", NormalizedUserName = "OFFLINE-REMINDER", DisplayName = "离线提醒用户" };
        var result = new StageResult
        {
            Project = project,
            Title = "失败草稿",
            ResultType = StageResultType.Progress,
            Status = StageResultStatus.Draft,
            ResultDate = new DateOnly(2026, 7, 16),
            SubmittedByUser = user,
            IsOfflineDraft = true
        };
        var sync = new OfflineDraftSync
        {
            User = user,
            ClientDraftId = Guid.NewGuid(),
            LastOperationId = Guid.NewGuid(),
            StageResult = result,
            LastServerVersion = result.ConcurrencyStamp,
            Status = OfflineSyncStatus.Failed,
            LastError = "照片上传失败"
        };
        fixture.Db.Add(sync);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.RefreshAsync(new DateOnly(2026, 7, 16), CancellationToken.None);
        var reminder = (await fixture.Service.ListAsync(false, CancellationToken.None)).Single(item => item.Type == ReminderType.OfflineSyncFailed);
        reminder.Message.Should().Contain("照片上传失败");

        sync.Status = OfflineSyncStatus.Synced;
        sync.LastError = null;
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.RefreshAsync(new DateOnly(2026, 7, 16), CancellationToken.None);

        (await fixture.Service.ListAsync(false, CancellationToken.None)).Should().NotContain(item => item.Id == reminder.Id);
        (await fixture.Service.ListAsync(true, CancellationToken.None)).Single(item => item.Id == reminder.Id).Status.Should().Be(ReminderStatus.Resolved);
    }

    [Fact]
    public async Task CertificatesUseThreeNaturalMonthReminderLevels()
    {
        await using var fixture = await ReminderFixture.CreateAsync();
        var employee = await fixture.Db.Employees.SingleAsync(item => item.EmployeeNumber == "REM-E");
        fixture.Db.EmployeeCertificates.AddRange(
            new EmployeeCertificate { Employee = employee, CertificateType = "轻度证书", ExpiresOn = new DateOnly(2026, 10, 16) },
            new EmployeeCertificate { Employee = employee, CertificateType = "中度证书", ExpiresOn = new DateOnly(2026, 9, 16) },
            new EmployeeCertificate { Employee = employee, CertificateType = "重度证书", ExpiresOn = new DateOnly(2026, 8, 16) },
            new EmployeeCertificate { Employee = employee, CertificateType = "长期证书", ExpiresOn = null });
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.RefreshAsync(new DateOnly(2026, 7, 17), default);
        var reminders = (await fixture.Service.ListAsync(false, default)).Where(item => item.Type == ReminderType.EmployeeCertificateExpiring).ToArray();

        reminders.Single(item => item.Message.Contains("轻度证书")).Severity.Should().Be(ReminderSeverity.Info);
        reminders.Single(item => item.Message.Contains("中度证书")).Severity.Should().Be(ReminderSeverity.Warning);
        reminders.Single(item => item.Message.Contains("重度证书")).Severity.Should().Be(ReminderSeverity.Critical);
        reminders.Should().NotContain(item => item.Message.Contains("长期证书"));
    }

    [Fact]
    public async Task RenewedOrDeletedCertificateAutomaticallyResolvesReminder()
    {
        await using var fixture = await ReminderFixture.CreateAsync();
        await fixture.Service.RefreshAsync(new DateOnly(2026, 7, 17), default);
        var companyReminder = (await fixture.Service.ListAsync(false, default)).Single(item => item.Type == ReminderType.CompanyCertificateExpiring);
        var employeeReminder = (await fixture.Service.ListAsync(false, default)).Single(item => item.Type == ReminderType.EmployeeCertificateExpiring);
        var companyCertificate = await fixture.Db.CompanyCertificates.SingleAsync();
        companyCertificate.ExpiresOn = new DateOnly(2027, 1, 1);
        var employeeCertificate = await fixture.Db.EmployeeCertificates.SingleAsync();
        employeeCertificate.IsDeleted = true;
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.RefreshAsync(new DateOnly(2026, 7, 17), default);

        var unresolved = await fixture.Service.ListAsync(false, default);
        unresolved.Should().NotContain(item => item.Id == companyReminder.Id || item.Id == employeeReminder.Id);
        var all = await fixture.Service.ListAsync(true, default);
        all.Single(item => item.Id == companyReminder.Id).Status.Should().Be(ReminderStatus.Resolved);
        all.Single(item => item.Id == employeeReminder.Id).Status.Should().Be(ReminderStatus.Resolved);
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
            var rentedEquipment = new Equipment { EquipmentNumber = "REM-EQ", Name = "提醒租赁设备", OwnershipType = EquipmentOwnershipType.Rented, LessorBusinessPartner = partner };
            rentedEquipment.LeaseAgreements.Add(new EquipmentLeaseAgreement { Equipment = rentedEquipment, LessorBusinessPartner = partner, StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 7, 20), RentMode = RentMode.Daily, UnitRate = 100m });
            rentedEquipment.MaintenanceRecords.Add(new EquipmentMaintenanceRecord { Equipment = rentedEquipment, NextDueDate = new DateOnly(2026, 7, 18) });
            db.Add(rentedEquipment);
            db.CompanyCertificates.Add(new CompanyCertificate { LegalEntity = legalEntity, CertificateType = "营业执照", CertificateNumber = "REM-LIC", ExpiresOn = new DateOnly(2026, 7, 20) });
            db.EmployeeCertificates.Add(new EmployeeCertificate { Employee = employee, CertificateType = "安全员证", CertificateNumber = "REM-SAFE", ExpiresOn = new DateOnly(2026, 8, 20) });
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
