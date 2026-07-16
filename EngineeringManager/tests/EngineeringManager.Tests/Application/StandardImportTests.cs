using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
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
    public async Task StandardTemplatesContainExpectedHeaders(ExportDataset dataset, string expectedHeader)
    {
        await using var fixture = await ImportFixture.CreateAsync();

        var file = await fixture.Service.GenerateTemplateAsync(dataset, CancellationToken.None);
        var sheet = SimpleXlsxReader.Read(file.Content).Single();

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
