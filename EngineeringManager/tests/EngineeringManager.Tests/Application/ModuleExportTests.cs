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
    [Theory]
    [InlineData(ExportDataset.Employees, "员工")]
    [InlineData(ExportDataset.Partners, "合作单位")]
    [InlineData(ExportDataset.Payroll, "工资")]
    [InlineData(ExportDataset.Collections, "收款")]
    [InlineData(ExportDataset.Accounts, "资金账户")]
    [InlineData(ExportDataset.Companies, "自有公司")]
    [InlineData(ExportDataset.CompanyAccounts, "公司账户")]
    [InlineData(ExportDataset.CompanyCertificates, "公司证照")]
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
            var legalEntity = new LegalEntity { Code = "MOD-LE", Name = "模块导出公司", ShortName = "导出公司" };
            var partner = new BusinessPartner { PartnerNumber = "MOD-BP", Name = "模块导出单位", ShortName = "导出单位" };
            partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = BusinessPartnerRoleType.ConstructionCrew });
            var employee = new Employee { EmployeeNumber = "MOD-E", Name = "模块导出员工", EmployeeType = EmployeeType.Formal, PositionTitle = "施工员" };
            var project = new Project { ProjectNumber = "MOD-P", Name = "模块导出项目", Stage = ProjectStage.UnderConstruction };
            project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = legalEntity, IsPrimary = true });
            var account = new FinancialAccount { LegalEntity = legalEntity, AccountName = "模块导出账户", AccountType = FinancialAccountType.Bank, OpeningBalance = 100m };
            var batch = new PayrollBatch { BatchNumber = "MOD-PAY", Name = "模块工资", BatchType = PayrollBatchType.Monthly, StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 7, 31), LegalEntity = legalEntity };
            batch.Items.Add(new PayrollItem { Batch = batch, Employee = employee, ItemType = PayrollItemType.FixedSalary, Nature = PayrollItemNature.Earning, Amount = 5000m });
            var certificate = new CompanyCertificate { LegalEntity = legalEntity, CertificateType = "营业执照", CertificateNumber = "MOD-LIC" };
            var equipment = new Equipment { EquipmentNumber = "MOD-EQ", Name = "模块导出设备", OwnershipType = EquipmentOwnershipType.SelfOwned, OwnerLegalEntity = legalEntity };
            equipment.ProjectUsages.Add(new EquipmentProjectUsage { Equipment = equipment, Project = project, LegalEntity = legalEntity, EntryDate = new DateOnly(2026, 7, 1), ExitDate = new DateOnly(2026, 7, 2), RentMode = RentMode.Daily, UnitRate = 100m });
            db.AddRange(legalEntity, partner, employee, project, account, batch, certificate, equipment);
            await db.SaveChangesAsync();
            var receivableId = await finance.AddReceivableAsync(new CreateReceivableRequest(project.Id, null, legalEntity.Id, partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 100m, null), CancellationToken.None);
            await finance.RecordCollectionAsync(new RecordCollectionRequest(receivableId, project.Id, null, legalEntity.Id, partner.Id, account.Id, new DateOnly(2026, 7, 2), 60m, PaymentMethod.BankTransfer, null), CancellationToken.None);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
