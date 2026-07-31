using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ModuleExportTests
{
    [Fact]
    public async Task SensitiveEmployeeFieldsAreMaskedUnlessExplicitlyAuthorized()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();
        var employee = await fixture.Db.Employees.SingleAsync();
        employee.IdentityNumber = "510101199001011234";
        employee.BankAccountNumber = "6222021234567890";
        employee.DefaultMonthlySalary = 8800m;
        await fixture.Db.SaveChangesAsync();

        var masked = await fixture.Service.ExportAsync(new ExportRequest(ExportDataset.Employees, "ordinary", ["identity_number", "bank_account_number", "default_monthly_salary"], null, CanViewSensitiveData: false), default);
        var complete = await fixture.Service.ExportAsync(new ExportRequest(ExportDataset.Employees, "sensitive-admin", ["identity_number", "bank_account_number", "default_monthly_salary"], null, CanViewSensitiveData: true), default);

        var maskedRow = SimpleXlsxReader.Read(masked.Content).Single().Rows[1];
        maskedRow.Should().NotContain(employee.IdentityNumber).And.NotContain(employee.BankAccountNumber).And.Contain("已脱敏");
        SimpleXlsxReader.Read(complete.Content).Single().Rows[1].Should().Equal(employee.IdentityNumber, employee.BankAccountNumber, 8800m);
    }

    [Fact]
    public async Task MultiModuleWorkbookContainsDirectoryAndModuleSheets()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();

        var file = await fixture.Service.ExportModulesAsync(new ExportModuleRequest(
            [ExportDataset.Employees, ExportDataset.Partners],
            "multi-module",
            new Dictionary<ExportDataset, IReadOnlyList<string>>
            {
                [ExportDataset.Employees] = ["employee_number", "name"],
                [ExportDataset.Partners] = ["partner_number", "name"]
            }), default);

        SimpleXlsxReader.Read(file.Content).Select(sheet => sheet.Name).Should().Equal("目录", "员工", "合作单位");
        (await fixture.Service.ListTasksAsync("multi-module", default)).Should().HaveCount(2).And.OnlyContain(task => task.Status == DataExchangeTaskStatus.Completed);
    }

    [Fact]
    public async Task ZipModuleExportContainsNavigationManifestAndChecksums()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();

        var file = await fixture.Service.ExportModulesAsync(new ExportModuleRequest(
            [ExportDataset.Employees, ExportDataset.Partners],
            "zip-module",
            new Dictionary<ExportDataset, IReadOnlyList<string>>(), PackageFormat: ExportPackageFormat.Zip), default);
        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(file.Content), System.IO.Compression.ZipArchiveMode.Read);
        archive.GetEntry("data-navigation.xlsx").Should().NotBeNull();
        archive.GetEntry("manifest.json").Should().NotBeNull();
        archive.GetEntry("checksums.sha256").Should().NotBeNull();
    }

    [Fact]
    public async Task CompanyAccountExportIncludesNotesValue()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();

        var file = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.CompanyAccounts, "notes-user", ["notes"], null),
            CancellationToken.None);
        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;

        rows[0].Should().Equal("备注");
        rows.Should().Contain(row => row.Contains("账户备注"));
    }

    [Fact]
    public async Task PayrollExportUsesUnifiedPersonLinesAndCategoryFields()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();
        var employee = await fixture.Db.Employees.FirstAsync();
        var company = await fixture.Db.LegalEntities.FirstAsync();
        var account = await fixture.Db.FinancialAccounts.FirstAsync();
        var batch = new PayrollBatch
        {
            BatchNumber = "MOD-UNIFIED-PAY",
            Name = "统一发放导出",
            BatchType = PayrollBatchType.Temporary,
            StartDate = new DateOnly(2026, 7, 18),
            EndDate = new DateOnly(2026, 7, 18),
            PaymentDate = new DateOnly(2026, 7, 18),
            LegalEntity = company,
            Account = account,
            ActualAmount = 800m,
            IsUnifiedDisbursement = true,
            Status = PayrollBatchStatus.Confirmed
        };
        batch.Payments.Add(new PayrollPayment { Batch = batch, RecipientType = PayrollRecipientType.Employee, RecipientKey = $"employee:{employee.Id:N}", Employee = employee, Amount = 800m, PayeeName = employee.Name, RecipientNameSnapshot = employee.Name });
        fixture.Db.PayrollBatches.Add(batch);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ExportRequest(ExportDataset.Payroll, "unified-payroll-export", ["payment_date", "recipient_type", "recipient_name", "amount", "actual_amount"], null), CancellationToken.None);
        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;

        rows[0].Should().Equal("发放日期", "人员来源", "人员姓名", "个人金额", "批次实际总额");
        rows.Should().Contain(row => row.SequenceEqual(new object?[] { new DateOnly(2026, 7, 18), "员工", employee.Name, 800m, 800m }));
    }

    [Fact]
    public async Task CashExportsIncludeDirectProjectEntriesWithoutDuplicatingAllocatedEntries()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();
        var company = await fixture.Db.LegalEntities.FirstAsync();
        var partner = await fixture.Db.BusinessPartners.FirstAsync();
        var account = await fixture.Db.FinancialAccounts.FirstAsync();
        var project = await fixture.Db.Projects.FirstAsync();

        fixture.Db.FinanceCashEntries.AddRange(
            new FinanceCashEntry
            {
                Scope = LedgerScope.External,
                Direction = LedgerDirection.Receivable,
                CashType = LedgerCashType.Collection,
                LegalEntity = company,
                BusinessPartner = partner,
                Project = project,
                Account = account,
                BusinessDate = new DateOnly(2026, 7, 5),
                Amount = 25m,
                PaymentMethod = "银行转账",
                Notes = "项目直接收款"
            },
            new FinanceCashEntry
            {
                Scope = LedgerScope.External,
                Direction = LedgerDirection.Payable,
                CashType = LedgerCashType.Payment,
                LegalEntity = company,
                BusinessPartner = partner,
                Project = project,
                Account = account,
                BusinessDate = new DateOnly(2026, 7, 6),
                Amount = 15m,
                PaymentMethod = "现金",
                Notes = "项目直接付款"
            });
        await fixture.Db.SaveChangesAsync();

        var collectionFile = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.Collections, "direct-cash", ["project_number", "amount", "notes"], null),
            CancellationToken.None);
        var paymentFile = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.Payments, "direct-cash", ["project_number", "amount", "notes"], null),
            CancellationToken.None);

        var collectionRows = SimpleXlsxReader.Read(collectionFile.Content).Single().Rows.Skip(1).ToArray();
        var paymentRows = SimpleXlsxReader.Read(paymentFile.Content).Single().Rows.Skip(1).ToArray();

        collectionRows.Should().HaveCount(2);
        collectionRows.Sum(row => Convert.ToDecimal(row[1], System.Globalization.CultureInfo.InvariantCulture)).Should().Be(85m);
        collectionRows.Should().Contain(row => row.Contains("项目直接收款"));
        paymentRows.Should().HaveCount(2);
        paymentRows.Sum(row => Convert.ToDecimal(row[1], System.Globalization.CultureInfo.InvariantCulture)).Should().Be(35m);
        paymentRows.Should().Contain(row => row.Contains("项目直接付款"));
    }

    [Fact]
    public async Task InvoiceExportUsesCentralLedgerAndChineseValues()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();
        var company = await fixture.Db.LegalEntities.FirstAsync();
        var partner = await fixture.Db.BusinessPartners.FirstAsync();
        var project = await fixture.Db.Projects.FirstAsync();
        fixture.Db.FinanceInvoices.Add(new FinanceInvoice
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            LegalEntity = company,
            BusinessPartner = partner,
            Project = project,
            InvoiceNumber = "CENTRAL-INV-001",
            InvoiceDate = new DateOnly(2026, 7, 7),
            Amount = 88m,
            Status = LedgerRecordStatus.Active,
            Notes = "中央发票"
        });
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.Invoices, "central-invoice", ["project_number", "invoice_number", "direction", "gross_amount", "status"], null),
            CancellationToken.None);
        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;

        rows.Should().Contain(row => row.SequenceEqual(new object?[] { project.ProjectNumber, "CENTRAL-INV-001", "应收", 88m, "有效" }));
    }

    [Fact]
    public async Task CentralLedgerExportsIncludeSystemIdsThatCanUpdateCashAndInvoices()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();
        var company = await fixture.Db.LegalEntities.FirstAsync();
        var partner = await fixture.Db.BusinessPartners.FirstAsync();
        var account = await fixture.Db.FinancialAccounts.FirstAsync();
        var project = await fixture.Db.Projects.FirstAsync();
        fixture.Db.FinanceCashEntries.Add(new FinanceCashEntry
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            CashType = LedgerCashType.Collection,
            LegalEntity = company,
            BusinessPartner = partner,
            Project = project,
            Account = account,
            BusinessDate = new DateOnly(2026, 7, 8),
            Amount = 41m,
            PaymentMethod = PaymentMethod.BankTransfer.ToString(),
            Notes = "系统ID回读收款"
        });
        fixture.Db.FinanceInvoices.Add(new FinanceInvoice
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            LegalEntity = company,
            BusinessPartner = partner,
            Project = project,
            InvoiceNumber = "SYSTEM-ID-INV",
            InvoiceDate = new DateOnly(2026, 7, 9),
            Amount = 51m,
            Status = LedgerRecordStatus.Active,
            Notes = "系统ID回读发票"
        });
        await fixture.Db.SaveChangesAsync();

        var cashFile = await fixture.Service.ExportAsync(new ExportRequest(
            ExportDataset.Collections,
            "system-id-export",
            ["_system_id", "project_number", "collection_date", "legal_entity_code", "partner_number", "account_number", "account", "amount", "payment_method", "notes"],
            null), CancellationToken.None);
        var invoiceFile = await fixture.Service.ExportAsync(new ExportRequest(
            ExportDataset.Invoices,
            "system-id-export",
            ["_system_id", "project_number", "invoice_number", "invoice_date", "direction", "legal_entity_code", "partner_number", "gross_amount", "status", "notes"],
            null), CancellationToken.None);

        var cashSheet = SimpleXlsxReader.Read(cashFile.Content).Single();
        var invoiceSheet = SimpleXlsxReader.Read(invoiceFile.Content).Single();
        cashSheet.Rows[0].Should().Contain("系统ID");
        invoiceSheet.Rows[0].Should().Contain("系统ID");

        var cashHeaders = cashSheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
        var cashRow = cashSheet.Rows.Single(row => row[cashHeaders.ToList().IndexOf("备注")]?.ToString() == "系统ID回读收款").ToArray();
        cashRow[cashHeaders.ToList().IndexOf("收款金额")] = 77m;
        var cashWorkbook = new SimpleXlsxWorkbook();
        cashWorkbook.AddWorksheet("收款导入", cashHeaders, [cashRow]);

        var invoiceHeaders = invoiceSheet.Rows[0].Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
        var invoiceRow = invoiceSheet.Rows.Single(row => row[invoiceHeaders.ToList().IndexOf("备注")]?.ToString() == "系统ID回读发票").ToArray();
        invoiceRow[invoiceHeaders.ToList().IndexOf("含税金额")] = 99m;
        var invoiceWorkbook = new SimpleXlsxWorkbook();
        invoiceWorkbook.AddWorksheet("发票导入", invoiceHeaders, [invoiceRow]);

        var importService = new ImportService(fixture.Db);
        var cashPreview = await importService.PreviewAsync(new ImportPreviewRequest("system-id-import", ExportDataset.Collections, "收款回读.xlsx", cashWorkbook.ToArray(), null, ImportMode.Update), CancellationToken.None);
        var invoicePreview = await importService.PreviewAsync(new ImportPreviewRequest("system-id-import", ExportDataset.Invoices, "发票回读.xlsx", invoiceWorkbook.ToArray(), null, ImportMode.Update), CancellationToken.None);
        cashPreview.Errors.Should().BeEmpty();
        invoicePreview.Errors.Should().BeEmpty();
        await importService.ConfirmAsync(cashPreview.BatchId, CancellationToken.None);
        await importService.ConfirmAsync(invoicePreview.BatchId, CancellationToken.None);

        (await fixture.Db.FinanceCashEntries.SingleAsync(item => item.Notes == "系统ID回读收款")).Amount.Should().Be(77m);
        (await fixture.Db.FinanceInvoices.SingleAsync(item => item.Notes == "系统ID回读发票")).Amount.Should().Be(99m);
    }

    [Fact]
    public async Task EmployeeLedgerExportsIncludeWagesAndReceiptsWithChineseLabels()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();
        var employee = await fixture.Db.Employees.FirstAsync();
        var company = await fixture.Db.LegalEntities.FirstAsync();
        var account = await fixture.Db.FinancialAccounts.FirstAsync();
        var year = new BusinessYear { Name = "2026年度", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31) };
        fixture.Db.Add(year);
        fixture.Db.EmployeeWageEntries.Add(new EmployeeWageEntry
        {
            Employee = employee,
            BusinessYear = year,
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2026, 8, 31),
            EntryType = EmployeeWageEntryType.Attendance,
            WageCategory = EmployeeWageCategory.SocialSecurityWage,
            CalculationMethod = EmployeeWageCalculationMethod.Monthly,
            Nature = PayrollItemNature.Earning,
            AutomaticAmount = 1000m,
            FinalAmount = 1000m,
            Notes = "工资导出"
        });
        fixture.Db.EmployeeOtherPayments.Add(new EmployeeOtherPayment
        {
            Employee = employee,
            LegalEntity = company,
            EntryType = EmployeeLedgerEntryType.Expense,
            RecordKind = EmployeeLedgerRecordKind.Payable,
            EntryDate = new DateOnly(2026, 8, 2),
            Amount = 200m,
            PaymentMethod = PaymentMethod.BankTransfer,
            Description = "往来导出"
        });
        fixture.Db.EmployeeReceipts.Add(new EmployeeReceipt
        {
            Employee = employee,
            BusinessYear = year,
            ReceiptDate = new DateOnly(2026, 8, 3),
            ReceiptType = EmployeeReceiptType.Wage,
            Amount = 800m,
            PaymentLegalEntity = company,
            Account = account,
            PaymentMethod = PaymentMethod.BankTransfer,
            ActualRecipientName = employee.Name,
            Notes = "收款导出"
        });
        await fixture.Db.SaveChangesAsync();

        var wageFile = await fixture.Service.ExportAsync(
            new ExportRequest((ExportDataset)21, "employee-ledger", ["employee_number", "entry_type", "final_amount"], null),
            CancellationToken.None);
        var otherFile = await fixture.Service.ExportAsync(
            new ExportRequest((ExportDataset)22, "employee-ledger", ["employee_number", "record_kind", "amount", "payment_method"], null),
            CancellationToken.None);
        var receiptFile = await fixture.Service.ExportAsync(
            new ExportRequest((ExportDataset)23, "employee-ledger", ["employee_number", "receipt_type", "amount", "payment_method"], null),
            CancellationToken.None);

        SimpleXlsxReader.Read(wageFile.Content).Single().Rows.Should().Contain(row => row.SequenceEqual(new object?[] { employee.EmployeeNumber, "出勤", 1000m }));
        SimpleXlsxReader.Read(otherFile.Content).Single().Rows.Should().Contain(row => row.SequenceEqual(new object?[] { employee.EmployeeNumber, "应付", 200m, "银行转账" }));
        SimpleXlsxReader.Read(receiptFile.Content).Single().Rows.Should().Contain(row => row.SequenceEqual(new object?[] { employee.EmployeeNumber, "工资", 800m, "银行转账" }));
    }

    [Fact]
    public async Task EmployeeExportUsesStableChineseTypeLabels()
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();
        fixture.Db.Employees.AddRange(
            new Employee { EmployeeNumber = "MOD-LABOR", Name = "劳务导出员工", EmployeeType = EmployeeType.Labor },
            new Employee { EmployeeNumber = "MOD-TEMP", Name = "临时导出员工", EmployeeType = EmployeeType.Temporary });
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.Employees, "employee-type-labels", ["employee_type"], null),
            CancellationToken.None);
        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;

        rows[0].Should().Equal("员工类型");
        rows.Skip(1).Select(row => (string)row.Single()!).Should().Equal(
            "正式员工",
            "劳务员工",
            "特殊临时人员");
    }

    [Theory]
    [InlineData(ExportDataset.ProjectOverview, "项目备注")]
    [InlineData(ExportDataset.Employees, "员工备注")]
    [InlineData(ExportDataset.Partners, "合作单位备注")]
    [InlineData(ExportDataset.Collections, "收款备注")]
    [InlineData(ExportDataset.Payments, "付款备注")]
    [InlineData(ExportDataset.Accounts, "账户备注")]
    [InlineData(ExportDataset.Companies, "公司备注")]
    [InlineData(ExportDataset.Equipment, "设备备注")]
    [InlineData(ExportDataset.EquipmentSettlements, "设备结算备注")]
    public async Task CoreModuleExportsIncludeNotesValues(ExportDataset dataset, string expectedNotes)
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();

        var file = await fixture.Service.ExportAsync(new ExportRequest(dataset, $"notes-{dataset}", ["notes"], null), CancellationToken.None);
        var rows = SimpleXlsxReader.Read(file.Content)[^1].Rows;

        rows[0].Should().Equal("备注");
        rows.Should().Contain(row => row.Contains(expectedNotes));
    }

    [Theory]
    [InlineData(ExportDataset.Employees, "员工")]
    [InlineData(ExportDataset.Partners, "合作单位")]
    [InlineData(ExportDataset.Payroll, "工资")]
    [InlineData(ExportDataset.Collections, "收款")]
    [InlineData(ExportDataset.Accounts, "资金账户")]
    [InlineData(ExportDataset.Companies, "自有公司")]
    [InlineData(ExportDataset.CompanyAccounts, "公司账户")]
    [InlineData(ExportDataset.CompanyCertificates, "公司证照")]
    [InlineData(ExportDataset.EmployeeCertificates, "员工证书")]
    [InlineData(ExportDataset.Equipment, "设备档案")]
    [InlineData(ExportDataset.EquipmentUsages, "设备使用")]
    public async Task MainModulesCanBeExportedWithIndependentSelections(ExportDataset dataset, string expectedSheet)
    {
        await using var fixture = await ModuleExportFixture.CreateAsync();

        var file = await fixture.Service.ExportAsync(new ExportRequest(dataset, "module-user", [], null), CancellationToken.None);
        var selection = await fixture.Service.GetLastSelectionAsync("module-user", dataset, CancellationToken.None);
        var sheets = SimpleXlsxReader.Read(file.Content);

        sheets.Should().ContainSingle();
        sheets[0].Name.Should().Be(expectedSheet);
        sheets[0].Rows.Count.Should().BeGreaterThan(1);
        selection.Should().NotBeNull();
        selection!.Dataset.Should().Be(dataset);
    }

    private sealed class ModuleExportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ModuleExportFixture(SqliteConnection connection, ApplicationDbContext db, IExportService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IExportService Service { get; }

        public static async Task<ModuleExportFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var finance = new FinanceLedgerService(db);
            var fixture = new ModuleExportFixture(connection, db, new ExportService(db, finance));
            var legalEntity = new LegalEntity { Code = "MOD-LE", Name = "模块导出公司", ShortName = "导出公司", Notes = "公司备注" };
            var partner = new BusinessPartner { PartnerNumber = "MOD-BP", Name = "模块导出单位", ShortName = "导出单位", Notes = "合作单位备注" };
            partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = BusinessPartnerRoleType.ConstructionCrew });
            var employee = new Employee { EmployeeNumber = "MOD-E", Name = "模块导出员工", EmployeeType = EmployeeType.Formal, PositionTitle = "施工员", Notes = "员工备注" };
            var project = new Project { ProjectNumber = "MOD-P", Name = "模块导出项目", Stage = ProjectStage.UnderConstruction, Notes = "项目备注" };
            project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = legalEntity, IsPrimary = true });
            var account = new FinancialAccount { LegalEntity = legalEntity, AccountName = "模块导出账户", AccountType = FinancialAccountType.Bank, OpeningBalance = 100m, Notes = "账户备注" };
            var batch = new PayrollBatch { BatchNumber = "MOD-PAY", Name = "模块工资", BatchType = PayrollBatchType.Monthly, StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 31), LegalEntity = legalEntity };
            batch.Items.Add(new PayrollItem { Batch = batch, Employee = employee, ItemType = PayrollItemType.FixedSalary, Nature = PayrollItemNature.Earning, Amount = 5000m });
            var certificate = new CompanyCertificate { LegalEntity = legalEntity, CertificateType = "营业执照", CertificateNumber = "MOD-LIC" };
            var employeeCertificate = new EmployeeCertificate { Employee = employee, CertificateType = "建造师证", CertificateNumber = "MOD-JZS" };
            var equipment = new Equipment { EquipmentNumber = "MOD-EQ", Name = "模块导出设备", OwnershipType = EquipmentOwnershipType.SelfOwned, OwnerLegalEntity = legalEntity, Notes = "设备备注" };
            var usage = new EquipmentProjectUsage { Equipment = equipment, Project = project, LegalEntity = legalEntity, EntryDate = new DateOnly(2026, 7, 1), ExitDate = new DateOnly(2026, 7, 2), RentMode = RentMode.Daily, UnitRate = 100m };
            equipment.ProjectUsages.Add(usage);
            var settlement = new EquipmentSettlement { Usage = usage, SettlementDate = new DateOnly(2026, 7, 2), BaseAmount = 200m, TotalAmount = 200m, ModificationReason = "测试", Notes = "设备结算备注" };
            db.AddRange(legalEntity, partner, employee, project, account, batch, certificate, employeeCertificate, equipment, settlement);
            await db.SaveChangesAsync();
            var receivableId = await finance.AddReceivableAsync(new CreateReceivableRequest(project.Id, null, legalEntity.Id, partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 100m, null), CancellationToken.None);
            await finance.RecordCollectionAsync(new RecordCollectionRequest(receivableId, project.Id, null, legalEntity.Id, partner.Id, account.Id, new DateOnly(2026, 7, 2), 60m, PaymentMethod.BankTransfer, "收款备注"), CancellationToken.None);
            var payableId = await finance.AddPayableAsync(new CreatePayableRequest(project.Id, null, legalEntity.Id, partner.Id, PayableSourceType.Manual, new DateOnly(2026, 7, 1), null, 50m, "测试应付"), CancellationToken.None);
            await finance.RecordPaymentAsync(new RecordPaymentRequest(payableId, project.Id, null, legalEntity.Id, partner.Id, account.Id, new DateOnly(2026, 7, 2), 20m, PaymentMethod.BankTransfer, "付款备注"), CancellationToken.None);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
