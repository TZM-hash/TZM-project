using System.Diagnostics;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Infrastructure.Equipment;
using EngineeringManager.Infrastructure.Finance;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Performance;

public sealed class RepresentativeDataPerformanceTests
{
    [Fact]
    public async Task RepresentativeListsStayWithinLocalBaseline()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        var company = new LegalEntity { Code = "PERF-C", Name = "性能测试公司", ShortName = "性能" }; db.LegalEntities.Add(company);
        var projectRows = Enumerable.Range(1, 100).Select(index => new Project { ProjectNumber = $"PERF-P-{index:000}", Name = $"性能测试项目 {index}", Stage = ProjectStage.UnderConstruction }).ToArray();
        foreach (var item in projectRows) item.LegalEntities.Add(new ProjectLegalEntity { Project = item, LegalEntity = company, IsPrimary = true });
        db.Projects.AddRange(projectRows);
        db.Employees.AddRange(Enumerable.Range(1, 500).Select(index => new Employee { EmployeeNumber = $"PERF-E-{index:0000}", Name = $"性能测试员工 {index}", EmployeeType = index % 2 == 0 ? EmployeeType.Formal : EmployeeType.Labor }));
        var equipmentRows = Enumerable.Range(1, 200).Select(index => new Equipment { EquipmentNumber = $"PERF-Q-{index:000}", Name = $"性能测试设备 {index}", OwnershipType = EquipmentOwnershipType.SelfOwned, OwnerLegalEntity = company }).ToArray();
        db.Equipment.AddRange(equipmentRows);
        await db.SaveChangesAsync();
        db.EquipmentProjectUsages.AddRange(Enumerable.Range(0, 10_000).Select(index => new EquipmentProjectUsage
        {
            Equipment = equipmentRows[index % equipmentRows.Length], Project = projectRows[index % projectRows.Length], LegalEntity = company,
            EntryDate = new DateOnly(2026, 1, index % 28 + 1), ExitDate = new DateOnly(2026, 1, index % 28 + 1), RentMode = RentMode.Daily, UnitRate = 800m,
            SharedUsageOverride = true, SharedUsageReason = "性能测试样例"
        }));
        await db.SaveChangesAsync();
        _ = await db.Projects.AsNoTracking().Take(50).ToListAsync();
        var watch = Stopwatch.StartNew();
        var projects = await db.Projects.AsNoTracking().OrderBy(item => item.ProjectNumber).Take(100).ToListAsync();
        var employees = await db.Employees.AsNoTracking().OrderBy(item => item.EmployeeNumber).Take(500).ToListAsync();
        var equipment = await db.Equipment.AsNoTracking().OrderBy(item => item.EquipmentNumber).Take(200).ToListAsync();
        watch.Stop();
        projects.Should().HaveCount(100); employees.Should().HaveCount(500); equipment.Should().HaveCount(200);
        (await db.EquipmentProjectUsages.CountAsync()).Should().Be(10_000);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));

        watch.Restart();
        var dashboard = await new EquipmentService(db).GetDashboardAsync(EquipmentActor.Administrator("performance"), new EquipmentFilter(null, null, null, null), default);
        watch.Stop();
        dashboard.TotalCount.Should().Be(200);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));

        watch.Restart();
        var export = await new ExportService(db, new FinanceLedgerService(db)).ExportAsync(new ExportRequest(ExportDataset.Equipment, "performance", [], null), default);
        watch.Stop();
        export.Content.Should().NotBeEmpty();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
    }
}
