using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectDateBackfillServiceTests
{
    [Fact]
    public async Task BackfillAsyncFillsOnlySafeEmptyFieldsAndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var project = new Project
        {
            ProjectNumber = "XM0208",
            Name = "甬金高速项目",
            Notes = "405 9号机，2025.3.10进场，2025.10.16退场；2025.11.01转账付款"
        };
        var confirmed = new Project
        {
            ProjectNumber = "XM0207",
            Name = "已有开工日期",
            ActualStartDate = new DateOnly(2024, 1, 1),
            Notes = "235机，2025.3.8进场，2025.3.30退场"
        };
        var unsafeProject = new Project
        {
            ProjectNumber = "XM0217",
            Name = "日期待核实",
            Notes = "山河240，2025.10.5进场，2025.2.1完工"
        };
        db.Projects.AddRange(project, confirmed, unsafeProject);
        await db.SaveChangesAsync();

        var service = new ProjectDateBackfillService(db);
        var first = await service.BackfillAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        var saved = await db.Projects.SingleAsync(item => item.Id == project.Id);
        saved.ActualStartDate.Should().Be(new DateOnly(2025, 3, 10));
        saved.ActualCompletionDate.Should().Be(new DateOnly(2025, 10, 16));
        var savedConfirmed = await db.Projects.SingleAsync(item => item.Id == confirmed.Id);
        savedConfirmed.ActualStartDate.Should().Be(new DateOnly(2024, 1, 1));
        savedConfirmed.ActualCompletionDate.Should().Be(new DateOnly(2025, 3, 30));
        var savedUnsafe = await db.Projects.SingleAsync(item => item.Id == unsafeProject.Id);
        savedUnsafe.ActualStartDate.Should().BeNull();
        savedUnsafe.ActualCompletionDate.Should().BeNull();

        first.TotalChanges.Should().Be(3);
        first.Items.Single(item => item.ProjectId == unsafeProject.Id).Warnings.Should().NotBeEmpty();
        (await db.AuditLogs.CountAsync(item => item.Action == "BackfillProjectDatesFromNotes")).Should().Be(1);

        var second = await new ProjectDateBackfillService(db).BackfillAsync(CancellationToken.None);

        second.TotalChanges.Should().Be(0);
        (await db.AuditLogs.CountAsync(item => item.Action == "BackfillProjectDatesFromNotes")).Should().Be(1);
    }
}
