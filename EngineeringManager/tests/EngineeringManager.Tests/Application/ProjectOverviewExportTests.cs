using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectOverviewExportTests
{
    [Fact]
    public async Task DefaultProjectOverviewContainsSummaryAndProjectDetails()
    {
        await using var fixture = await ExportFixture.CreateAsync();

        var file = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.ProjectOverview, "leader-1", [], new DateOnly(2026, 7, 31)),
            CancellationToken.None);
        var sheets = SimpleXlsxReader.Read(file.Content);

        file.FileName.Should().EndWith(".xlsx");
        sheets.Select(item => item.Name).Should().Equal("总览汇总", "项目明细");
        sheets[0].Rows.Should().Contain(row => row.Count > 0 && Equals(row[0], "未收款"));
        sheets[1].Rows[0].Should().Contain("项目编号");
        sheets[1].Rows.Should().Contain(row => row.Count > 1 && row.Contains(fixture.Project.ProjectNumber));
    }

    [Fact]
    public async Task FieldOrderAndLastSelectionAreSavedPerUserAndDataset()
    {
        await using var fixture = await ExportFixture.CreateAsync();
        var selectedFields = new[] { "uncollected_amount", "project_number" };

        var file = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.ProjectOverview, "leader-2", selectedFields, null),
            CancellationToken.None);
        var lastSelection = await fixture.Service.GetLastSelectionAsync("leader-2", ExportDataset.ProjectOverview, CancellationToken.None);
        var sheets = SimpleXlsxReader.Read(file.Content);

        sheets[1].Rows[0].Should().Equal("未收款", "项目编号");
        lastSelection.Should().NotBeNull();
        lastSelection!.SelectedFields.Should().Equal(selectedFields);
    }

    [Fact]
    public async Task ProjectOverviewCanExportContinuousSerialNumbers()
    {
        await using var fixture = await ExportFixture.CreateAsync();

        var second = new Project { ProjectNumber = "EXPORT-Q", Name = "第二个导出项目", Stage = ProjectStage.UnderConstruction };
        fixture.Db.Projects.Add(second);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.ProjectOverview, "leader-serial", ["serial_number", "project_name"], null),
            CancellationToken.None);
        var details = SimpleXlsxReader.Read(file.Content)[1].Rows;

        details[0].Should().Equal("序号", "项目名称");
        details.Skip(1).Select(row => row[0]).Should().Equal(1d, 2d);
    }

    [Fact]
    public async Task CurrentViewExportRestrictsRowsToMatchingProjects()
    {
        await using var fixture = await ExportFixture.CreateAsync();
        var hidden = new Project { ProjectNumber = "EXPORT-HIDDEN", Name = "不在当前筛选中的项目", Stage = ProjectStage.UnderConstruction };
        fixture.Db.Projects.Add(hidden);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(
            new ExportRequest(ExportDataset.ProjectOverview, "leader-filter", ["project_number", "project_name"], null, [fixture.Project.Id]),
            CancellationToken.None);
        var details = SimpleXlsxReader.Read(file.Content)[1].Rows;

        details.Should().Contain(row => row.Contains(fixture.Project.ProjectNumber));
        details.Should().NotContain(row => row.Contains(hidden.ProjectNumber));
    }

    [Fact]
    public async Task PersonalAndAdministratorSharedTemplatesCanBeSaved()
    {
        await using var fixture = await ExportFixture.CreateAsync();

        await fixture.Service.SaveTemplateAsync(new SaveExportTemplateRequest("leader-3", "领导项目表", ExportDataset.ProjectOverview, ExportTemplateScope.Personal, ["project_number", "uncollected_amount"], null, false), CancellationToken.None);
        await fixture.Service.SaveTemplateAsync(new SaveExportTemplateRequest("admin-1", "共享经营总览", ExportDataset.ProjectOverview, ExportTemplateScope.Shared, ["project_number", "project_name", "uncollected_amount"], null, true), CancellationToken.None);

        var templates = await fixture.Service.ListTemplatesAsync("leader-3", ExportDataset.ProjectOverview, CancellationToken.None);
        templates.Should().Contain(item => item.Name == "领导项目表" && item.Scope == ExportTemplateScope.Personal);
        templates.Should().Contain(item => item.Name == "共享经营总览" && item.Scope == ExportTemplateScope.Shared);
    }

    private sealed class ExportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ExportFixture(SqliteConnection connection, ApplicationDbContext db, IExportService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public IExportService Service { get; }
        public Project Project { get; private set; } = null!;

        public static async Task<ExportFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var financeService = new FinanceLedgerService(db);
            var fixture = new ExportFixture(connection, db, new ExportService(db, financeService));
            var legalEntity = new LegalEntity { Code = "EXPORT-LE", Name = "导出测试公司", ShortName = "导出公司" };
            var partner = new BusinessPartner { PartnerNumber = "EXPORT-BP", Name = "导出测试客户", ShortName = "导出客户" };
            fixture.Project = new Project { ProjectNumber = "EXPORT-P", Name = "导出测试项目", Stage = ProjectStage.UnderConstruction };
            fixture.Project.LegalEntities.Add(new ProjectLegalEntity { Project = fixture.Project, LegalEntity = legalEntity, IsPrimary = true });
            db.AddRange(legalEntity, partner, fixture.Project);
            await db.SaveChangesAsync();
            await financeService.AddReceivableAsync(new CreateReceivableRequest(fixture.Project.Id, null, legalEntity.Id, partner.Id, ReceivableSourceType.Manual, new DateOnly(2026, 7, 1), null, 100m, null), CancellationToken.None);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
