using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class DataExchangeTaskServiceTests
{
    [Fact]
    public async Task HistoryCombinesImportsAndExportsInNewestFirstOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        fixture.Db.DataExchangeTasks.AddRange(
            new DataExchangeTask
            {
                UserId = "owner",
                Direction = DataExchangeDirection.Export,
                DatasetsJson = "[5]",
                Status = DataExchangeTaskStatus.Completed,
                RowCount = 2,
                FileName = "员工.xlsx",
                ResultContent = [1],
                CreatedAt = now.AddMinutes(-3),
                CompletedAt = now.AddMinutes(-3)
            },
            new DataExchangeTask
            {
                UserId = "owner",
                Direction = DataExchangeDirection.Export,
                DatasetsJson = "[2]",
                Status = DataExchangeTaskStatus.Completed,
                RowCount = 1,
                FileName = "项目.xlsx",
                ResultContent = [2],
                CreatedAt = now.AddMinutes(-1),
                CompletedAt = now.AddMinutes(-1)
            });
        var batch = new ImportBatch
        {
            CreatedByUserId = "owner",
            Dataset = ExportDataset.Employees,
            Mode = ImportMode.Mixed,
            OriginalFileName = "员工补充.xlsx",
            OriginalContent = [3],
            Status = DataExchangeTaskStatus.PreviewReady,
            TotalRows = 4,
            ValidRows = 3,
            ErrorRows = 1,
            CreatedAt = now
        };
        batch.Errors.Add(new ImportError { Batch = batch, RowNumber = 2, ColumnName = "姓名", Message = "必填字段不能为空。", RawValue = null });
        fixture.Db.ImportBatches.Add(batch);
        await fixture.Db.SaveChangesAsync();

        var page = await fixture.Service.ListAsync(new("owner", CanManage: false, Page: 1, PageSize: 20), default);

        page.Items.Select(item => item.FileName).Should().Equal("员工补充.xlsx", "项目.xlsx", "员工.xlsx");
        page.Items[0].Direction.Should().Be(DataExchangeDirection.Import);
        page.Items[0].ErrorRowCount.Should().Be(1);
        page.Items[0].CanDownloadErrors.Should().BeTrue();
        page.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task HistoryIsScopedToOwnerUnlessManagerAndPageSizeIsLimitedToSupportedValues()
    {
        await using var fixture = await Fixture.CreateAsync();
        for (var index = 0; index < 25; index++)
        {
            fixture.Db.DataExchangeTasks.Add(new DataExchangeTask
            {
                UserId = index == 24 ? "other" : "owner",
                Direction = DataExchangeDirection.Export,
                DatasetsJson = "[5]",
                Status = DataExchangeTaskStatus.Completed,
                ResultContent = [1],
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-index)
            });
        }
        await fixture.Db.SaveChangesAsync();

        var ownerPage = await fixture.Service.ListAsync(new("owner", CanManage: false, PageSize: 50), default);
        var managerPage = await fixture.Service.ListAsync(new("owner", CanManage: true, PageSize: 100), default);

        ownerPage.TotalCount.Should().Be(24);
        ownerPage.Items.Should().HaveCount(24);
        ownerPage.PageSize.Should().Be(50);
        managerPage.TotalCount.Should().Be(25);
        managerPage.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task DownloadsRequireOwnerOrManagerAndErrorReportIsAnExcelWorkbook()
    {
        await using var fixture = await Fixture.CreateAsync();
        var export = new DataExchangeTask
        {
            UserId = "owner",
            Direction = DataExchangeDirection.Export,
            DatasetsJson = "[5]",
            Status = DataExchangeTaskStatus.Completed,
            FileName = "员工.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ResultContent = [1, 2, 3]
        };
        var batch = new ImportBatch
        {
            CreatedByUserId = "owner",
            Dataset = ExportDataset.Employees,
            OriginalFileName = "员工补充.xlsx",
            OriginalContent = [4],
            Status = DataExchangeTaskStatus.PreviewReady,
            TotalRows = 1,
            ValidRows = 0,
            ErrorRows = 1
        };
        batch.Errors.Add(new ImportError { Batch = batch, RowNumber = 2, ColumnName = "姓名", Message = "姓名不能为空。", RawValue = "" });
        fixture.Db.DataExchangeTasks.Add(export);
        fixture.Db.ImportBatches.Add(batch);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.DownloadExportAsync("owner", false, export.Id, default);
        file.FileName.Should().Be("员工.xlsx");
        file.Content.Should().Equal(1, 2, 3);
        var report = await fixture.Service.DownloadImportErrorsAsync("admin", true, batch.Id, default);
        SimpleXlsxReader.Read(report.Content).Select(item => item.Name).Should().Contain("错误报告");
        SimpleXlsxReader.Read(report.Content).Single(item => item.Name == "错误报告").Rows[1].Should().Contain("姓名");
        await FluentActions.Invoking(() => fixture.Service.DownloadExportAsync("other", false, export.Id, default))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db, DataExchangeTaskService service) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public DataExchangeTaskService Service { get; } = service;

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db, new DataExchangeTaskService(db));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
