using System.Security.Claims;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Pages.Employees;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Tests.Web;

public sealed class EmployeeListSortingTests
{
    [Fact]
    public async Task DefaultSortPlacesTheNewestEmployeeNumberOnTheFirstPage()
    {
        var service = new StubEmployeeService(Enumerable.Range(1, 21)
            .Select(index => Employee($"YG{index:0000}", index == 21 ? "最新员工" : $"员工{index:00}"))
            .ToArray());
        var model = new IndexModel(service)
        {
            PageSize = 20,
            PageContext = AdministratorPageContext()
        };

        await model.OnGetAsync(CancellationToken.None);

        model.Employees.Should().HaveCount(20);
        model.Employees[0].EmployeeNumber.Should().Be("YG0021");
    }

    private static EmployeeDto Employee(string number, string name) =>
        new(
            Guid.NewGuid(),
            number,
            name,
            EmployeeType.Formal,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            [],
            ConcurrencyStamp: Guid.NewGuid());

    private static PageContext AdministratorPageContext()
    {
        var identity = new ClaimsIdentity("EmployeeSortingTest", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "employee-sort-admin"));
        identity.AddClaim(new Claim(ClaimTypes.Role, SystemRoles.SystemAdministrator));
        return new PageContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private sealed class StubEmployeeService(IReadOnlyList<EmployeeDto> employees) : IEmployeeService
    {
        public Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, CancellationToken cancellationToken) =>
            Task.FromResult(employees);

        public Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto> CopyAsync(CopyEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto> UpdateAsync(string userId, UpdateEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeAffiliationDto> AddAffiliationAsync(CreateEmployeeAffiliationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
