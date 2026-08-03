using System.Security.Cryptography;
using System.Text;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class DataExchangeRoundTripContractTests
{
    [Fact]
    public void ImportSourceMetadataCapturesRoundTripIdentity()
    {
        var sourceExportTaskId = Guid.NewGuid();
        var sourceBytes = Encoding.UTF8.GetBytes("round-trip");
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes));

        var metadata = new ImportSourceMetadata(
            ImportSourceType.SystemExport,
            sourceExportTaskId,
            "employees/1",
            sourceSha256);

        metadata.SourceType.Should().Be(ImportSourceType.SystemExport);
        metadata.SourceExportTaskId.Should().Be(sourceExportTaskId);
        metadata.DatasetVersion.Should().Be("employees/1");
        metadata.SourceSha256.Should().Be(sourceSha256);
    }

    [Fact]
    public void ImportBatchStoresSourceMetadataForAudit()
    {
        var sourceExportTaskId = Guid.NewGuid();
        var batch = new ImportBatch
        {
            SourceType = ImportSourceType.SystemExport,
            SourceExportTaskId = sourceExportTaskId,
            DatasetVersion = "projects/1",
            SourceSha256 = "ABC123"
        };

        batch.SourceType.Should().Be(ImportSourceType.SystemExport);
        batch.SourceExportTaskId.Should().Be(sourceExportTaskId);
        batch.DatasetVersion.Should().Be("projects/1");
        batch.SourceSha256.Should().Be("ABC123");
    }

    [Fact]
    public void ExportTaskStoresDatasetVersionAndSourcePage()
    {
        var task = new DataExchangeTask
        {
            DatasetVersion = "projects/1",
            SourcePage = "/Projects"
        };

        task.DatasetVersion.Should().Be("projects/1");
        task.SourcePage.Should().Be("/Projects");
    }

    [Fact]
    public async Task ExportServicePersistsDatasetVersionAndSourcePage()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();

        await fixture.ExportService.ExportAsync(
            new ExportRequest(
                ExportDataset.Employees,
                "round-trip-user",
                [],
                null,
                DatasetVersion: "employees/1",
                SourcePage: "/Employees"),
            CancellationToken.None);

        var task = await fixture.Db.DataExchangeTasks.SingleAsync();
        task.DatasetVersion.Should().Be("employees/1");
        task.SourcePage.Should().Be("/Employees");
    }

    [Fact]
    public async Task ImportServicePersistsProvidedSourceMetadata()
    {
        await using var fixture = await ExchangeFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("员工导入", ["员工编号", "姓名", "员工类型"], [["RT-001", "往返员工", "正式员工"]]);
        var sourceBytes = Encoding.UTF8.GetBytes("source-export");
        var sourceMetadata = new ImportSourceMetadata(
            ImportSourceType.SystemExport,
            Guid.NewGuid(),
            "employees/1",
            Convert.ToHexString(SHA256.HashData(sourceBytes)));

        var preview = await fixture.ImportService.PreviewAsync(
            new ImportPreviewRequest(
                "round-trip-user",
                ExportDataset.Employees,
                "员工往返.xlsx",
                workbook.ToArray(),
                null,
                SourceMetadata: sourceMetadata),
            CancellationToken.None);

        var batch = await fixture.Db.ImportBatches.SingleAsync(item => item.Id == preview.BatchId);
        batch.SourceType.Should().Be(ImportSourceType.SystemExport);
        batch.SourceExportTaskId.Should().Be(sourceMetadata.SourceExportTaskId);
        batch.DatasetVersion.Should().Be("employees/1");
        batch.SourceSha256.Should().Be(sourceMetadata.SourceSha256);
    }

    private sealed class ExchangeFixture(SqliteConnection connection, ApplicationDbContext db, ExportService exportService, ImportService importService) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public ExportService ExportService { get; } = exportService;
        public ImportService ImportService { get; } = importService;

        public static async Task<ExchangeFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new ExchangeFixture(connection, db, new ExportService(db, new FinanceLedgerService(db)), new ImportService(db));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
