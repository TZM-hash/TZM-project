using EngineeringManager.Application.TemporaryWorkers;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.TemporaryWorkers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Application;

public sealed class TemporaryWorkerServiceTests
{
    [Fact]
    public async Task OptionalIdentityAndPotentialDuplicateDoNotBlockCreation()
    {
        await using var fixture = await TemporaryWorkerFixture.CreateAsync();

        var first = await fixture.Service.CreateAsync("admin", new CreateTemporaryWorkerRequest("临时张三", null, "13800000000", null, null, "杂工", null, null, "首次建档"), CancellationToken.None);
        var second = await fixture.Service.CreateAsync("admin", new CreateTemporaryWorkerRequest("临时张三", null, null, null, null, "杂工", null, null, "重名但不同人"), CancellationToken.None);

        first.HasPotentialDuplicate.Should().BeFalse();
        second.HasPotentialDuplicate.Should().BeTrue();
        (await fixture.Db.TemporaryWorkers.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task TemporaryWorkerCanLinkToConvertedEmployeeWithoutChangingOriginalRecord()
    {
        await using var fixture = await TemporaryWorkerFixture.CreateAsync();
        var worker = await fixture.Service.CreateAsync("admin", new CreateTemporaryWorkerRequest("转正人员", null, null, null, null, null, null, null, "临时建档"), CancellationToken.None);
        var employee = new Employee { EmployeeNumber = "TEMP-CONVERT-E", Name = "转正人员", EmployeeType = EmployeeType.Formal };
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.LinkConvertedEmployeeAsync("admin", worker.Id, employee.Id, "转为正式员工", CancellationToken.None);

        var entity = await fixture.Db.TemporaryWorkers.SingleAsync(item => item.Id == worker.Id);
        entity.ConvertedEmployeeId.Should().Be(employee.Id);
        entity.Name.Should().Be("转正人员");
    }

    private sealed class TemporaryWorkerFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private TemporaryWorkerFixture(SqliteConnection connection, ApplicationDbContext db, ITemporaryWorkerService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public ITemporaryWorkerService Service { get; }

        public static async Task<TemporaryWorkerFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new TemporaryWorkerFixture(connection, db, new TemporaryWorkerService(db));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
