using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Certificates;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Infrastructure.Certificates;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class CertificateManagementServiceTests
{
    [Fact]
    public async Task EmployeeCertificateCanBeCreatedUpdatedAndSoftDeletedWithAudit()
    {
        await using var fixture = await CertificateFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "CERT-E", Name = "证书员工", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();

        var saved = await fixture.EmployeeService.SaveAsync("admin", true, new SaveEmployeeCertificateRequest(
            null, employee.Id, "建造师证", "JZS-100", "建筑工程一级", "住建部门", new DateOnly(2020, 1, 1), new DateOnly(2026, 9, 1), null, false, "初始", null, "新增证书"), new DateOnly(2026, 7, 17), default);
        var updated = await fixture.EmployeeService.SaveAsync("admin", true, new SaveEmployeeCertificateRequest(
            saved.Id, employee.Id, "建造师证", "JZS-100", "建筑工程一级", "住建部门", saved.IssuedOn, null, null, false, "续期后长期有效", saved.ConcurrencyStamp, "证书续期"), new DateOnly(2026, 7, 17), default);
        await fixture.EmployeeService.DeleteAsync("admin", true, updated.Id, updated.ConcurrencyStamp, "证书作废", default);

        (await fixture.Db.EmployeeCertificates.IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeTrue();
        (await fixture.Db.AuditLogs.Where(item => item.EntityType == nameof(EmployeeCertificate)).ToListAsync()).Select(item => item.Action).Should().Contain(["Create", "Update", "Delete"]);
    }

    [Fact]
    public async Task CertificateDateAndManagePermissionAreValidated()
    {
        await using var fixture = await CertificateFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "CERT-DATE", Name = "日期员工", EmployeeType = EmployeeType.Labor };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();
        var request = new SaveEmployeeCertificateRequest(null, employee.Id, "操作员证", null, null, null, new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 1), null, false, null, null, "测试");

        await fixture.EmployeeService.Invoking(service => service.SaveAsync("reader", false, request, new DateOnly(2026, 7, 17), default)).Should().ThrowAsync<UnauthorizedAccessException>();
        await fixture.EmployeeService.Invoking(service => service.SaveAsync("admin", true, request, new DateOnly(2026, 7, 17), default)).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CompanyCertificateRespectsCompanyScopeAndAllowsSameTypeMultipleTimes()
    {
        await using var fixture = await CertificateFixture.CreateAsync();
        var first = new LegalEntity { Code = "CERT-C1", Name = "第一公司", ShortName = "一公司" };
        var second = new LegalEntity { Code = "CERT-C2", Name = "第二公司", ShortName = "二公司" };
        fixture.Db.AddRange(first, second);
        await fixture.Db.SaveChangesAsync();
        var admin = CompanyActor.Administrator("admin");
        await fixture.CompanyService.SaveAsync(admin, new SaveCompanyCertificateItemRequest(null, first.Id, "资质证书", "ZZ-1", "建筑二级", "省住建厅", null, null, null, false, null, null, "新增"), new DateOnly(2026, 7, 17), default);
        await fixture.CompanyService.SaveAsync(admin, new SaveCompanyCertificateItemRequest(null, first.Id, "资质证书", "ZZ-2", "市政二级", "省住建厅", null, null, null, false, null, null, "新增"), new DateOnly(2026, 7, 17), default);

        (await fixture.CompanyService.ListAsync(new CompanyActor("reader", false, false, [first.Id]), new CertificateFilter(), new DateOnly(2026, 7, 17), default)).Should().HaveCount(2);
        (await fixture.CompanyService.ListAsync(new CompanyActor("reader", false, false, [second.Id]), new CertificateFilter(), new DateOnly(2026, 7, 17), default)).Should().BeEmpty();
    }

    [Fact]
    public async Task CertificateAttachmentCanBeUploadedDownloadedAndRemoved()
    {
        await using var fixture = await CertificateFixture.CreateAsync();
        var employee = new Employee { EmployeeNumber = "CERT-FILE", Name = "附件员工", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();
        var content = "certificate-file-content"u8.ToArray();
        var saved = await fixture.EmployeeService.SaveAsync("admin", true, new SaveEmployeeCertificateRequest(
            null, employee.Id, "操作员证", "FILE-01", null, null, null, null,
            new CertificateAttachmentUpload("操作员证.pdf", "application/pdf", content), false, null, null, "上传附件"), new DateOnly(2026, 7, 17), default);

        var downloaded = await fixture.EmployeeService.DownloadAttachmentAsync(saved.Id, default);
        downloaded.OriginalFileName.Should().Be("操作员证.pdf");
        downloaded.Content.Should().Equal(content);

        var updated = await fixture.EmployeeService.SaveAsync("admin", true, new SaveEmployeeCertificateRequest(
            saved.Id, employee.Id, saved.CertificateType, saved.CertificateNumber, null, null, null, null,
            null, true, null, saved.ConcurrencyStamp, "删除附件"), new DateOnly(2026, 7, 17), default);
        updated.AttachmentId.Should().BeNull();
        await fixture.EmployeeService.Invoking(service => service.DownloadAttachmentAsync(saved.Id, default)).Should().ThrowAsync<KeyNotFoundException>();
    }

    private sealed class CertificateFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly string attachmentRoot;
        private CertificateFixture(SqliteConnection connection, string attachmentRoot, ApplicationDbContext db, EmployeeCertificateService employeeService, CompanyCertificateService companyService)
        {
            this.connection = connection;
            this.attachmentRoot = attachmentRoot;
            Db = db;
            EmployeeService = employeeService;
            CompanyService = companyService;
        }
        public ApplicationDbContext Db { get; }
        public EmployeeCertificateService EmployeeService { get; }
        public CompanyCertificateService CompanyService { get; }
        public static async Task<CertificateFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var root = Path.Combine(Path.GetTempPath(), $"certificate-tests-{Guid.NewGuid():N}");
            var store = new LocalFileStore(root);
            return new CertificateFixture(connection, root, db, new EmployeeCertificateService(db, store), new CompanyCertificateService(db, store));
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
            if (Directory.Exists(attachmentRoot)) Directory.Delete(attachmentRoot, recursive: true);
        }
    }
}
