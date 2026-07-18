using System.Reflection;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using LegacyDetailsModel = EngineeringManager.Web.Pages.TemporaryWorkers.DetailsModel;
using LegacyIndexModel = EngineeringManager.Web.Pages.TemporaryWorkers.IndexModel;

namespace EngineeringManager.Tests.Web;

public sealed class TemporaryWorkerPageTests
{
    [Fact]
    public void LegacyIndexGetRedirectsToTemporaryEmployees()
    {
        var model = new LegacyIndexModel();

        var redirect = model.OnGet().Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.PageName.Should().Be("/Employees/Index");
        redirect.RouteValues.Should().ContainKey("employeeType").WhoseValue.Should().Be(EmployeeType.Temporary);
    }

    [Fact]
    public void LegacyIndexHasNoPostHandlers()
    {
        typeof(LegacyIndexModel).GetMethods()
            .Should().NotContain(method => method.Name.StartsWith("OnPost", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyDetailsUsesTheSameRoleRestrictionAsLegacyIndex()
    {
        var indexAuthorization = typeof(LegacyIndexModel).GetCustomAttribute<AuthorizeAttribute>();
        var detailsAuthorization = typeof(LegacyDetailsModel).GetCustomAttribute<AuthorizeAttribute>();

        indexAuthorization.Should().NotBeNull();
        detailsAuthorization.Should().NotBeNull();
        detailsAuthorization!.Roles.Should().Be(indexAuthorization!.Roles);
    }

    [Fact]
    public async Task LegacyDetailsGetRedirectsToMappedEmployee()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        var employee = new Employee
        {
            EmployeeNumber = "LEGACY-REDIRECT-001",
            Name = "Legacy redirect employee",
            EmployeeType = EmployeeType.Temporary
        };
        var legacyId = Guid.NewGuid();
        db.PersonnelMigrationMaps.Add(new PersonnelMigrationMap
        {
            LegacyTemporaryWorkerId = legacyId,
            Employee = employee
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var model = new LegacyDetailsModel(db);

        var result = await model.OnGetAsync(legacyId, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.PageName.Should().Be("/Employees/Details");
        redirect.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(employee.Id);
    }

    [Fact]
    public async Task LegacyDetailsGetReturnsMigrationMessageWhenMapIsMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();
        var model = new LegacyDetailsModel(db);

        var result = await model.OnGetAsync(Guid.NewGuid(), CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().BeOfType<string>().Which.Should().Contain("迁移");
    }

    [Fact]
    public void LegacyRazorPagesDoNotRenderIndependentFormsOrHistoryLists()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "TemporaryWorkers", "Index.cshtml");
        var details = ReadFile("src", "EngineeringManager.Web", "Pages", "TemporaryWorkers", "Details.cshtml");

        index.Should().Contain("@page")
            .And.NotContain("<form")
            .And.NotContain("历史发放合计")
            .And.NotContain("Model.Workers");
        details.Should().Contain("@page")
            .And.NotContain("<form")
            .And.NotContain("历次发放记录")
            .And.NotContain("查看来源批次")
            .And.NotContain("Model.Worker");
    }

    private static ApplicationDbContext CreateDbContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
