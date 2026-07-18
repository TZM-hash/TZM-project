using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.EmployeeAnnualLedger;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class BusinessYearServiceTests
{
    [Fact]
    public async Task CurrentBusinessYearUsesCustomDateRangeAndRejectsOverlap()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var service = new BusinessYearService(db);
        var first = await service.CreateAsync(
            new CreateBusinessYearRequest("2026经营年度", new DateOnly(2026, 3, 1), new DateOnly(2027, 2, 28)),
            CancellationToken.None);

        var current = await service.GetByDateAsync(new DateOnly(2027, 1, 15), CancellationToken.None);
        var overlapping = () => service.CreateAsync(
            new CreateBusinessYearRequest("重叠年度", new DateOnly(2027, 2, 28), new DateOnly(2028, 2, 29)),
            CancellationToken.None);

        current.Should().BeEquivalentTo(first);
        await overlapping.Should().ThrowAsync<InvalidOperationException>().WithMessage("*重叠*");
    }
}
