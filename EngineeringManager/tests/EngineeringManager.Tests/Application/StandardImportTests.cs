using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class StandardImportTests
{
    [Theory]
    [InlineData(ExportDataset.Employees, "员工编号")]
    [InlineData(ExportDataset.Partners, "单位编号")]
    [InlineData(ExportDataset.Projects, "项目编号")]
    [InlineData(ExportDataset.Companies, "公司编码")]
    [InlineData(ExportDataset.CompanyAccounts, "公司编码")]
    [InlineData(ExportDataset.CompanyCertificates, "公司编码")]
    [InlineData(ExportDataset.EmployeeCertificates, "员工编号")]
    [InlineData(ExportDataset.Equipment, "设备编号")]
    [InlineData(ExportDataset.EquipmentLeases, "设备编号")]
    [InlineData(ExportDataset.EquipmentUsages, "设备编号")]
    [InlineData(ExportDataset.EquipmentPeriods, "设备编号")]
    [InlineData(ExportDataset.EquipmentSettlements, "设备编号")]
    public async Task StandardTemplatesContainExpectedHeaders(ExportDataset dataset, string expectedHeader)
    {
        await using var fixture = await ImportFixture.CreateAsync();

        var file = await fixture.Service.GenerateTemplateAsync(dataset, CancellationToken.None);
        var sheet = SimpleXlsxReader.Read(file.Content)[0];

        sheet.Rows[0].Should().Contain(expectedHeader);
    }

    [Fact]
    public async Task PreviewReturnsRowErrorsAndDoesNotPartiallyImport()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("员工导入", ["员工编号", "姓名", "员工类型"], [["E-001", "张三", "正式员工"], ["E-002", "", "劳务员工"]]);

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user-1", ExportDataset.Employees, "员工.xlsx", workbook.ToArray(), null), CancellationToken.None);
        var confirmAction = () => fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        preview.TotalRows.Should().Be(2);
        preview.ValidRows.Should().Be(1);
        preview.Errors.Should().ContainSingle(item => item.RowNumber == 3 && item.ColumnName == "姓名");
        await confirmAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*错误*");
        (await fixture.Db.Employees.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BatchSpecificOldSystemMappingCanBeConfirmed()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("旧员工", ["工号", "人员姓名", "人员类别"], [["OLD-001", "旧系统员工", "正式员工"]]);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["工号"] = "employee_number",
            ["人员姓名"] = "name",
            ["人员类别"] = "employee_type"
        };

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user-2", ExportDataset.Employees, "旧系统员工.xlsx", workbook.ToArray(), mapping), CancellationToken.None);
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        var employee = await fixture.Db.Employees.SingleAsync();
        employee.EmployeeNumber.Should().Be("OLD-001");
        employee.Name.Should().Be("旧系统员工");
        var batch = await fixture.Db.ImportBatches.SingleAsync(item => item.Id == preview.BatchId);
        batch.OriginalContent.Should().NotBeEmpty();
        batch.Status.Should().Be(DataExchangeTaskStatus.Completed);
    }

    [Fact]
    public async Task EmployeeImportAcceptsStableChineseAndEnglishTypeLabels()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet(
            "员工导入",
            ["员工编号", "姓名", "员工类型"],
            [
                ["TYPE-FORMAL-CN", "正式中文", "正式员工"],
                ["TYPE-FORMAL-EN", "正式英文", "Formal"],
                ["TYPE-LABOR-CN", "劳务中文", "劳务员工"],
                ["TYPE-LABOR-EN", "劳务英文", "Labor"],
                ["TYPE-TEMP-CN", "临时中文", "特殊临时人员"],
                ["TYPE-TEMP-EN", "临时英文", "Temporary"]
            ]);

        var preview = await fixture.Service.PreviewAsync(
            new ImportPreviewRequest("type-labels", ExportDataset.Employees, "员工类型.xlsx", workbook.ToArray(), null),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(preview.BatchId, CancellationToken.None);
        var imported = await fixture.Db.Employees.ToDictionaryAsync(item => item.EmployeeNumber, item => item.EmployeeType);
        imported["TYPE-FORMAL-CN"].Should().Be(EmployeeType.Formal);
        imported["TYPE-FORMAL-EN"].Should().Be(EmployeeType.Formal);
        imported["TYPE-LABOR-CN"].Should().Be(EmployeeType.Labor);
        imported["TYPE-LABOR-EN"].Should().Be(EmployeeType.Labor);
        imported["TYPE-TEMP-CN"].Should().Be(EmployeeType.Temporary);
        imported["TYPE-TEMP-EN"].Should().Be(EmployeeType.Temporary);
    }

    [Fact]
    public async Task MixedImportUpdatesExistingEmployeeAndPreservesAllOrNothingOnConcurrencyConflict()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "UPDATE-001", Name = "旧姓名", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("员工", ["员工编号", "姓名", "员工类型", "并发版本"], [[employee.EmployeeNumber, "新姓名", "正式员工", employee.ConcurrencyStamp.ToString()]]);
        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("update-user", ExportDataset.Employees, "update.xlsx", workbook.ToArray(), null, ImportMode.Mixed), default);
        await fixture.Service.ConfirmAsync(preview.BatchId, default);
        (await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id)).Name.Should().Be("新姓名");

        var stale = new SimpleXlsxWorkbook();
        stale.AddWorksheet("员工", ["员工编号", "姓名", "员工类型", "并发版本"], [[employee.EmployeeNumber, "不应写入", "正式员工", Guid.NewGuid().ToString()]]);
        var stalePreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("update-user", ExportDataset.Employees, "stale.xlsx", stale.ToArray(), null, ImportMode.Update), default);
        var before = (await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id)).Name;
        var confirmStale = () => fixture.Service.ConfirmAsync(stalePreview.BatchId, default);
        stalePreview.Errors.Should().ContainSingle(item => item.ColumnName == "并发版本");
        await confirmStale.Should().ThrowAsync<InvalidOperationException>().WithMessage("*错误*");
        (await fixture.Db.Employees.SingleAsync(item => item.Id == employee.Id)).Name.Should().Be(before);
    }

    [Fact]
    public async Task CompanyAccountAndCertificateImportsResolveCompanyAndCategory()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.CompanyCategories.Add(new EngineeringManager.Domain.Organization.CompanyCategory { Code = "GENERAL", Name = "一般纳税人有限公司" });
        await fixture.Db.SaveChangesAsync();
        var companyBook = new SimpleXlsxWorkbook();
        companyBook.AddWorksheet("公司导入", ["公司编码", "公司全称", "公司简称", "组合分类编码", "法人/经营者", "统一社会信用代码/税号"], [["IMP-C", "导入测试公司", "导入公司", "GENERAL", "测试法人", "913IMP"]]);

        var companyPreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.Companies, "公司.xlsx", companyBook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(companyPreview.BatchId, default);
        var company = await fixture.Db.LegalEntities.SingleAsync(item => item.Code == "IMP-C");

        var accountBook = new SimpleXlsxWorkbook();
        accountBook.AddWorksheet("公司账户导入", ["公司编码", "账户名称", "账户类型", "期初余额", "默认收款", "默认付款", "默认开票"], [["IMP-C", "基本户", "银行", "100", "是", "是", "是"]]);
        var accountPreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.CompanyAccounts, "账户.xlsx", accountBook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(accountPreview.BatchId, default);

        var certificateBook = new SimpleXlsxWorkbook();
        certificateBook.AddWorksheet("公司证照导入", ["公司编码", "资料类型", "资料编号", "有效期"], [["IMP-C", "营业执照", "LIC-01", "2030-12-31"]]);
        var certificatePreview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.CompanyCertificates, "证照.xlsx", certificateBook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(certificatePreview.BatchId, default);

        company.CompanyCategoryId.Should().NotBeNull();
        (await fixture.Db.FinancialAccounts.SingleAsync()).IsDefaultInvoice.Should().BeTrue();
        (await fixture.Db.CompanyCertificates.SingleAsync()).ExpiresOn.Should().Be(new DateOnly(2030, 12, 31));
    }

    [Fact]
    public async Task EquipmentImportResolvesOwnerCompany()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.LegalEntities.Add(new EngineeringManager.Domain.Organization.LegalEntity { Code = "EQ-OWNER", Name = "设备所属公司", ShortName = "设备公司" });
        await fixture.Db.SaveChangesAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("设备导入", ["设备编号", "设备名称", "权属", "所属公司编码", "型号"], [["IMP-EQ", "导入挖掘机", "自有", "EQ-OWNER", "X100"]]);
        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.Equipment, "设备.xlsx", workbook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(preview.BatchId, default);
        var equipment = await fixture.Db.Equipment.SingleAsync();
        equipment.EquipmentNumber.Should().Be("IMP-EQ");
        equipment.OwnerLegalEntityId.Should().NotBeNull();
    }

    [Fact]
    public async Task EmployeeCertificateImportResolvesEmployeeAndExtendedFields()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.Employees.Add(new Employee { EmployeeNumber = "CERT-EMP", Name = "导入持证员工", EmployeeType = EngineeringManager.Domain.Employees.EmployeeType.Formal });
        await fixture.Db.SaveChangesAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("员工证书导入", ["员工编号", "证书类型", "证书编号", "专业/等级/范围", "发证机关", "签发日期", "到期日期"], [["CERT-EMP", "安全员证", "AQ-100", "C证", "住建部门", "2024-01-01", "2027-01-01"]]);

        var preview = await fixture.Service.PreviewAsync(new ImportPreviewRequest("user", ExportDataset.EmployeeCertificates, "员工证书.xlsx", workbook.ToArray(), null), default);
        await fixture.Service.ConfirmAsync(preview.BatchId, default);

        var certificate = await fixture.Db.EmployeeCertificates.SingleAsync();
        certificate.SpecialtyLevelScope.Should().Be("C证");
        certificate.IssuingAuthority.Should().Be("住建部门");
    }

    private sealed class ImportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ImportFixture(SqliteConnection connection, ApplicationDbContext db, IImportService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IImportService Service { get; }

        public static async Task<ImportFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new ImportFixture(connection, db, new ImportService(db));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
