using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class UnifiedPersonnelDataExchangeTests
{
    [Fact]
    public async Task EmployeeExportContainsUnifiedIdentityColumnsAndValues()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var person = new Person
        {
            PersonNumber = "PER-EXPORT-001",
            Name = "导出员工",
            Employee = new Employee
            {
                EmployeeNumber = "YG-EXPORT-001",
                Name = "导出员工",
                EmployeeType = EmployeeType.Formal
            }
        };
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.Internal,
            InternalType = EmployeeType.Formal,
            StartDate = new DateOnly(2020, 1, 1),
            IsPrimary = true,
            Reason = "测试"
        });
        fixture.Db.People.Add(person);
        await fixture.Db.SaveChangesAsync();

        var exported = await fixture.ExportService.ExportAsync(
            new ExportRequest(
                ExportDataset.Employees,
                "test-user",
                ["employee_number", "person_number", "personnel_scope", "_person_id"],
                null),
            CancellationToken.None);

        var sheet = SimpleXlsxReader.Read(exported.Content).Single(item => item.Name == "员工");
        sheet.Rows[0].Should().Equal("员工编号", "统一人员编号", "当前人员分类", "人员主档ID");
        sheet.Rows[1].Should().Equal("YG-EXPORT-001", "PER-EXPORT-001", "内部人员", person.Id.ToString());
    }

    [Fact]
    public async Task EmployeeImportCreatesPersonBridgeAndInternalHistory()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工导入",
            ["员工编号", "统一人员编号", "当前人员分类", "姓名", "员工类型", "电话", "身份证号"],
            [["YG-IMPORT-001", "PER-IMPORT-001", "内部人员", "导入员工", "正式员工", "13800000001", "3301-2000 0101"]]);

        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "test-user",
                ExportDataset.Employees,
                "员工导入.xlsx",
                workbook.ToArray(),
                null),
            CancellationToken.None);
        preview.Errors.Should().BeEmpty();

        await fixture.ImportService.ConfirmAsync(preview.BatchId, CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        var employee = await fixture.Db.Employees.Include(item => item.Person).SingleAsync();
        employee.PersonId.Should().NotBeNull();
        employee.Person.Should().NotBeNull();
        employee.Person!.PersonNumber.Should().Be("PER-IMPORT-001");
        employee.Person.Name.Should().Be("导入员工");
        employee.Person.Phone.Should().Be("13800000001");
        employee.Person.IdentityNumberNormalized.Should().Be("330120000101");
        var history = await fixture.Db.PersonnelEngagementHistories.SingleAsync();
        history.PersonId.Should().Be(employee.PersonId!.Value);
        history.Scope.Should().Be(PersonnelScope.Internal);
        history.InternalType.Should().Be(EmployeeType.Formal);
        history.IsPrimary.Should().BeTrue();
        history.Reason.Should().Be("员工导入");
    }

    [Fact]
    public async Task EmployeeImportResolvesByPersonNumberAndSynchronizesAllPublicProfiles()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var sharedPerson = new Person
        {
            PersonNumber = "PER-SHARED-001",
            Name = "原姓名",
            Employee = new Employee
            {
                EmployeeNumber = "YG-SHARED-001",
                Name = "原姓名",
                EmployeeType = EmployeeType.Labor
            },
            ConstructionWorker = new ConstructionWorker { Name = "原姓名" }
        };
        var decoyPerson = new Person
        {
            PersonNumber = "PER-DECOY-001",
            Name = "干扰员工",
            Employee = new Employee
            {
                EmployeeNumber = "YG-DECOY-001",
                Name = "干扰员工",
                EmployeeType = EmployeeType.Formal
            }
        };
        fixture.Db.People.AddRange(sharedPerson, decoyPerson);
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工导入",
            ["员工编号", "统一人员编号", "姓名", "员工类型", "电话", "身份证号", "银行卡号", "开户行", "系统ID"],
            [[
                "YG-SHARED-001",
                "PER-SHARED-001",
                "统一后姓名",
                "正式员工",
                "13900000002",
                "3301-2000 0202",
                "62220002",
                "测试银行",
                decoyPerson.Employee!.Id.ToString()
            ]]);

        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "test-user",
                ExportDataset.Employees,
                "员工更新.xlsx",
                workbook.ToArray(),
                null),
            CancellationToken.None);
        preview.Errors.Should().BeEmpty();

        await fixture.ImportService.ConfirmAsync(preview.BatchId, CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        var updated = await fixture.Db.People
            .Include(item => item.Employee)
            .Include(item => item.ConstructionWorker)
            .SingleAsync(item => item.PersonNumber == "PER-SHARED-001");
        updated.Name.Should().Be("统一后姓名");
        updated.Phone.Should().Be("13900000002");
        updated.IdentityNumberNormalized.Should().Be("330120000202");
        updated.BankAccountNumber.Should().Be("62220002");
        updated.BankName.Should().Be("测试银行");
        updated.Employee!.Name.Should().Be("统一后姓名");
        updated.Employee.EmployeeType.Should().Be(EmployeeType.Formal);
        updated.ConstructionWorker!.Name.Should().Be("统一后姓名");
        updated.ConstructionWorker.IdentityNumber.Should().Be("3301-2000 0202");

        var decoy = await fixture.Db.People.Include(item => item.Employee)
            .SingleAsync(item => item.PersonNumber == "PER-DECOY-001");
        decoy.Name.Should().Be("干扰员工");
        decoy.Employee!.Name.Should().Be("干扰员工");
    }

    [Fact]
    public async Task EmployeeImportPreviewReportsNormalizedIdentityConflictWithoutMergingPeople()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var identityOwner = new Person
        {
            PersonNumber = "PER-ID-OWNER",
            Name = "证件持有人",
            IdentityNumber = "330120000303",
            IdentityNumberNormalized = "330120000303",
            Employee = new Employee
            {
                EmployeeNumber = "YG-ID-OWNER",
                Name = "证件持有人",
                IdentityNumber = "330120000303",
                EmployeeType = EmployeeType.Formal
            }
        };
        var importTarget = new Person
        {
            PersonNumber = "PER-ID-TARGET",
            Name = "导入目标",
            Employee = new Employee
            {
                EmployeeNumber = "YG-ID-TARGET",
                Name = "导入目标",
                EmployeeType = EmployeeType.Labor
            }
        };
        fixture.Db.People.AddRange(identityOwner, importTarget);
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工导入",
            ["员工编号", "统一人员编号", "姓名", "员工类型", "身份证号"],
            [["YG-ID-TARGET", "PER-ID-TARGET", "导入目标", "劳务员工", "3301-2000 0303"]]);

        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "test-user",
                ExportDataset.Employees,
                "身份证冲突.xlsx",
                workbook.ToArray(),
                null),
            CancellationToken.None);

        preview.Errors.Should().ContainSingle(error =>
            error.ColumnName == "身份证号" && error.Message.Contains("身份证号冲突", StringComparison.Ordinal));
        (await fixture.Db.People.CountAsync()).Should().Be(2);
        (await fixture.Db.Employees.SingleAsync(item => item.EmployeeNumber == "YG-ID-TARGET"))
            .PersonId.Should().Be(importTarget.Id);
    }

    [Fact]
    public async Task PayrollCrewWorkerExportIncludesUnifiedIdentityAndCrewNumber()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var crew = new BusinessPartner
        {
            PartnerNumber = "BZ-EXPORT-001",
            Name = "导出班组",
            ShortName = "导出班组"
        };
        var person = new Person
        {
            PersonNumber = "PER-CREW-EXPORT",
            Name = "班组工人",
            ConstructionWorker = new ConstructionWorker { Name = "班组工人" }
        };
        person.EngagementHistory.Add(new PersonnelEngagementHistory
        {
            Person = person,
            Scope = PersonnelScope.External,
            ExternalType = ExternalPersonnelType.ConstructionCrew,
            BusinessPartner = crew,
            CrewBusinessPartner = crew,
            StartDate = new DateOnly(2020, 1, 1),
            IsPrimary = true,
            Reason = "测试"
        });
        var batch = new PayrollBatch
        {
            BatchNumber = "PAY-CREW-EXPORT",
            Name = "班组工资",
            BatchType = PayrollBatchType.Monthly,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 31),
            PaymentDate = new DateOnly(2026, 8, 1),
            ActualAmount = 1200m,
            IsUnifiedDisbursement = true
        };
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.CrewWorker,
            ConstructionWorker = person.ConstructionWorker,
            CrewBusinessPartner = crew,
            RecipientKey = $"crew-worker:{person.ConstructionWorker!.Id:N}",
            Amount = 1200m,
            PayeeType = PayrollPayeeType.CrewLeader,
            PayeeName = "班组工人",
            RecipientNameSnapshot = "班组工人",
            CrewNameSnapshot = "导出班组"
        });
        fixture.Db.AddRange(crew, person, batch);
        await fixture.Db.SaveChangesAsync();

        var exported = await fixture.ExportService.ExportAsync(
            new ExportRequest(
                ExportDataset.Payroll,
                "test-user",
                ["recipient_type", "person_number", "personnel_scope", "_person_id", "crew_number", "recipient_name", "amount"],
                null),
            CancellationToken.None);

        var sheet = SimpleXlsxReader.Read(exported.Content).Single(item => item.Name == "工资");
        sheet.Rows[0].Should().Equal("人员来源", "统一人员编号", "当前人员分类", "人员主档ID", "班组编号", "人员姓名", "个人金额");
        sheet.Rows[1].Should().Equal("班组工人", "PER-CREW-EXPORT", "外部人员", person.Id.ToString(), "BZ-EXPORT-001", "班组工人", 1200m);
    }

    [Fact]
    public async Task PayrollCrewWorkerImportPreservesExistingPersonWorkerAndCrew()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var crew = new BusinessPartner
        {
            PartnerNumber = "BZ-IMPORT-001",
            Name = "导入班组",
            ShortName = "导入班组"
        };
        var person = new Person
        {
            PersonNumber = "PER-CREW-IMPORT",
            Name = "导入班组工人",
            ConstructionWorker = new ConstructionWorker { Name = "导入班组工人" }
        };
        person.ConstructionWorker!.Memberships.Add(new ConstructionCrewMembership
        {
            Worker = person.ConstructionWorker,
            CrewBusinessPartner = crew,
            StartDate = new DateOnly(2020, 1, 1),
            IsPrimary = true
        });
        fixture.Db.AddRange(crew, person);
        await fixture.Db.SaveChangesAsync();
        var originalWorkerId = person.ConstructionWorker.Id;

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "工资导入",
            [
                "批次编号", "批次名称", "批次类型", "开始日期", "结束日期", "发放日期", "实际总额",
                "人员来源", "统一人员编号", "人员主档ID", "班组编号", "人员姓名", "个人金额"
            ],
            [[
                "PAY-CREW-IMPORT", "导入班组工资", "按月", "2026-07-01", "2026-07-31", "2026-08-01", 1500m,
                "班组工人", "PER-CREW-IMPORT", person.Id.ToString(), "BZ-IMPORT-001", "导入班组工人", 1500m
            ]]);

        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "test-user",
                ExportDataset.Payroll,
                "班组工资导入.xlsx",
                workbook.ToArray(),
                null),
            CancellationToken.None);
        preview.Errors.Should().BeEmpty();

        await fixture.ImportService.ConfirmAsync(preview.BatchId, CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        var payment = await fixture.Db.PayrollPayments
            .Include(item => item.ConstructionWorker)
            .SingleAsync();
        payment.RecipientType.Should().Be(PayrollRecipientType.CrewWorker);
        payment.ConstructionWorkerId.Should().Be(originalWorkerId);
        payment.ConstructionWorker!.PersonId.Should().Be(person.Id);
        payment.CrewBusinessPartnerId.Should().Be(crew.Id);
        (await fixture.Db.People.CountAsync()).Should().Be(1);
        (await fixture.Db.ConstructionWorkers.CountAsync()).Should().Be(1);
        (await fixture.Db.BusinessPartners.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task LegacyRepairReportsNormalizedIdentityConflictWithoutMergingPersonMasters()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var employeePerson = new Person
        {
            PersonNumber = "PER-LEGACY-EMPLOYEE",
            Name = "历史员工",
            IdentityNumber = "3301-2000 0404",
            Employee = new Employee
            {
                EmployeeNumber = "YG-LEGACY-001",
                Name = "历史员工",
                IdentityNumber = "3301-2000 0404",
                EmployeeType = EmployeeType.Formal
            }
        };
        var workerPerson = new Person
        {
            PersonNumber = "PER-LEGACY-WORKER",
            Name = "历史工人",
            IdentityNumber = "330120000404",
            ConstructionWorker = new ConstructionWorker
            {
                Name = "历史工人",
                IdentityNumber = "330120000404"
            }
        };
        fixture.Db.People.AddRange(employeePerson, workerPerson);
        await fixture.Db.SaveChangesAsync();

        var result = await new LegacyDataRepairService(fixture.Db).RepairAsync(CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        result.PersonnelConflicts.Should().ContainSingle(conflict =>
            conflict.NormalizedIdentityNumber == "330120000404"
            && conflict.PersonIds.Contains(employeePerson.Id)
            && conflict.PersonIds.Contains(workerPerson.Id));
        (await fixture.Db.People.CountAsync()).Should().Be(2);
        (await fixture.Db.Employees.SingleAsync()).PersonId.Should().Be(employeePerson.Id);
        (await fixture.Db.ConstructionWorkers.SingleAsync()).PersonId.Should().Be(workerPerson.Id);
    }

    [Fact]
    public async Task CompleteEmployeeWorkbookDoesNotMergeDifferentPeopleByName()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var existing = new Person
        {
            PersonNumber = "PER-SAME-NAME-OLD",
            Name = "同名人员",
            IdentityNumber = "OLD-ID-001",
            IdentityNumberNormalized = "OLDID001",
            Employee = new Employee
            {
                EmployeeNumber = "YG0001",
                Name = "同名人员",
                IdentityNumber = "OLD-ID-001",
                EmployeeType = EmployeeType.Formal
            }
        };
        fixture.Db.People.Add(existing);
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工总表",
            ["姓名", "身份证号", "实际应付工资", "已付", "未付"],
            [["同名人员", "NEW-ID-002", 0m, 0m, 0m]]);

        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "test-user",
                ExportDataset.Employees,
                "完整员工工作簿.xlsx",
                workbook.ToArray(),
                null),
            CancellationToken.None);
        preview.Errors.Should().BeEmpty();

        await fixture.ImportService.ConfirmAsync(preview.BatchId, CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        (await fixture.Db.People.CountAsync()).Should().Be(2);
        (await fixture.Db.Employees.CountAsync()).Should().Be(2);
        var unchanged = await fixture.Db.People.SingleAsync(item => item.PersonNumber == "PER-SAME-NAME-OLD");
        unchanged.IdentityNumber.Should().Be("OLD-ID-001");
        var imported = await fixture.Db.People
            .Include(item => item.Employee)
            .SingleAsync(item => item.IdentityNumberNormalized == "NEWID002");
        imported.Employee.Should().NotBeNull();
        imported.Employee!.EmployeeNumber.Should().Be("YG0002");
        (await fixture.Db.PersonnelEngagementHistories.SingleAsync(item => item.PersonId == imported.Id))
            .Scope.Should().Be(PersonnelScope.Internal);
    }

    [Fact]
    public async Task PayrollExportCanBePreviewedWithoutRenamingRoundTripHeaders()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var crew = new BusinessPartner
        {
            PartnerNumber = "BZ-ROUNDTRIP-001",
            Name = "往返班组",
            ShortName = "往返班组"
        };
        var person = new Person
        {
            PersonNumber = "PER-CREW-ROUNDTRIP",
            Name = "往返工人",
            ConstructionWorker = new ConstructionWorker { Name = "往返工人" }
        };
        person.ConstructionWorker!.Memberships.Add(new ConstructionCrewMembership
        {
            Worker = person.ConstructionWorker,
            CrewBusinessPartner = crew,
            StartDate = new DateOnly(2020, 1, 1),
            IsPrimary = true
        });
        var batch = new PayrollBatch
        {
            BatchNumber = "PAY-CREW-ROUNDTRIP",
            Name = "往返班组工资",
            BatchType = PayrollBatchType.Monthly,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 31),
            PaymentDate = new DateOnly(2026, 8, 1),
            ActualAmount = 1600m,
            IsUnifiedDisbursement = true
        };
        batch.Payments.Add(new PayrollPayment
        {
            Batch = batch,
            RecipientType = PayrollRecipientType.CrewWorker,
            ConstructionWorker = person.ConstructionWorker,
            CrewBusinessPartner = crew,
            RecipientKey = $"crew-worker:{person.ConstructionWorker.Id:N}",
            Amount = 1600m,
            PayeeType = PayrollPayeeType.CrewLeader,
            PayeeName = "往返工人",
            RecipientNameSnapshot = "往返工人",
            CrewNameSnapshot = "往返班组"
        });
        fixture.Db.AddRange(crew, person, batch);
        await fixture.Db.SaveChangesAsync();

        var exported = await fixture.ExportService.ExportAsync(
            new ExportRequest(ExportDataset.Payroll, "test-user", [], null),
            CancellationToken.None);
        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "test-user",
                ExportDataset.Payroll,
                exported.FileName,
                exported.Content,
                null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task EmployeeImportPreviewReportsIdentityConflictWithinWorkbook()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工导入",
            ["员工编号", "统一人员编号", "姓名", "员工类型", "身份证号"],
            [
                ["YG-FILE-ID-001", "PER-FILE-ID-001", "文件人员甲", "正式员工", "3301-2000 0505"],
                ["YG-FILE-ID-002", "PER-FILE-ID-002", "文件人员乙", "劳务员工", "330120000505"]
            ]);

        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "test-user",
                ExportDataset.Employees,
                "文件内身份证冲突.xlsx",
                workbook.ToArray(),
                null),
            CancellationToken.None);

        preview.Errors.Should().Contain(error =>
            error.RowNumber == 3
            && error.ColumnName == "身份证号"
            && error.Message.Contains("身份证号冲突", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PayrollRoundTripRestoresProjectCompanyAccountAndPaymentMethodByCodes()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var company = new LegalEntity
        {
            Code = "GS-ROUNDTRIP",
            Name = "往返公司",
            ShortName = "往返公司"
        };
        var account = new FinancialAccount
        {
            LegalEntity = company,
            AccountName = "往返账户",
            AccountNumber = "ACCT-ROUNDTRIP",
            AccountType = FinancialAccountType.Bank
        };
        var project = new Project
        {
            ProjectNumber = "XM-ROUNDTRIP",
            Name = "往返项目",
            Stage = ProjectStage.UnderConstruction
        };
        project.LegalEntities.Add(new ProjectLegalEntity
        {
            Project = project,
            LegalEntity = company,
            IsPrimary = true
        });
        var batch = new PayrollBatch
        {
            BatchNumber = "PAY-CODE-ROUNDTRIP",
            Name = "编号往返工资",
            BatchType = PayrollBatchType.Monthly,
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 31),
            PaymentDate = new DateOnly(2026, 8, 1),
            Project = project,
            LegalEntity = company,
            Account = account,
            ActualAmount = 1800m,
            PaymentMethod = PaymentMethod.WeChat
        };
        fixture.Db.AddRange(company, account, project, batch);
        await fixture.Db.SaveChangesAsync();

        var exported = await fixture.ExportService.ExportAsync(
            new ExportRequest(
                ExportDataset.Payroll,
                "test-user",
                [
                    "batch_number", "batch_name", "batch_type", "start_date", "end_date", "payment_date",
                    "project_number", "legal_entity_code", "account_number", "actual_amount", "payment_method"
                ],
                null),
            CancellationToken.None);

        batch.ProjectId = null;
        batch.Project = null;
        batch.LegalEntityId = null;
        batch.LegalEntity = null;
        batch.AccountId = null;
        batch.Account = null;
        batch.PaymentMethod = PaymentMethod.Cash;
        await fixture.Db.SaveChangesAsync();

        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "test-user",
                ExportDataset.Payroll,
                exported.FileName,
                exported.Content,
                null),
            CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.ImportService.ConfirmAsync(preview.BatchId, CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();

        var restored = await fixture.Db.PayrollBatches.SingleAsync(item => item.Id == batch.Id);
        restored.ProjectId.Should().Be(project.Id);
        restored.LegalEntityId.Should().Be(company.Id);
        restored.AccountId.Should().Be(account.Id);
        restored.PaymentMethod.Should().Be(PaymentMethod.WeChat);
    }

    private sealed class ExchangeFixture(
        SqliteConnection connection,
        ApplicationDbContext db,
        ExportService exportService,
        ImportService importService) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public ExportService ExportService { get; } = exportService;
        public ImportService ImportService { get; } = importService;

        public static async Task<ExchangeFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new ExchangeFixture(
                connection,
                db,
                new ExportService(db, new FinanceLedgerService(db)),
                new ImportService(db));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
