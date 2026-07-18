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
        rows.Should().Contain(row => row.SequenceEqual(new object?[] { new DateOnly(2026, 7, 18), "Employee", employee.Name, 800m, 800m }));
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
