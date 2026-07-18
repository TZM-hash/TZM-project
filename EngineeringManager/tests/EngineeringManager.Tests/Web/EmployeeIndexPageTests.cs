using System.Reflection;
using System.Security.Claims;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Pages.Employees;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Tests.Web;

public sealed class EmployeeIndexPageTests
{
    [Fact]
    public void EmployeeTypeFilterIsAnOptionalGetBinding()
    {
        AssertOptionalGetBinding("EmployeeType", typeof(EmployeeType?));
    }

    [Fact]
    public void SearchFilterIsAnOptionalGetBinding()
    {
        AssertOptionalGetBinding("Search", typeof(string));
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(EmployeeType.Formal, 1)]
    [InlineData(EmployeeType.Labor, 1)]
    [InlineData(EmployeeType.Temporary, 1)]
    public async Task EmployeeTypeFilterIsAppliedAfterListing(EmployeeType? employeeType, int expectedCount)
    {
        var service = new RecordingEmployeeService();
        var model = new IndexModel(service) { EmployeeType = employeeType };

        await model.OnGetAsync(CancellationToken.None);

        service.LastSearch.Should().BeNull();
        model.Employees.Should().HaveCount(expectedCount);
        if (employeeType.HasValue)
        {
            model.Employees.Should().OnlyContain(employee => employee.EmployeeType == employeeType.Value);
        }
    }

    [Fact]
    public async Task SearchAndEmployeeTypeFiltersAreAppliedTogether()
    {
        var service = new RecordingEmployeeService();
        var model = new IndexModel(service)
        {
            Search = "MATCH",
            EmployeeType = EmployeeType.Temporary
        };

        await model.OnGetAsync(CancellationToken.None);

        service.LastSearch.Should().Be("MATCH");
        model.Employees.Should().ContainSingle()
            .Which.EmployeeType.Should().Be(EmployeeType.Temporary);
    }

    [Fact]
    public async Task SuccessfulQuickEditRedirectPreservesEmployeeType()
    {
        var service = new RecordingEmployeeService();
        var model = CreateAdministratorModel(service);
        model.EmployeeType = EmployeeType.Temporary;
        model.QuickEdit = CreateQuickEdit(service.TemporaryEmployee);

        var result = await model.OnPostQuickEditAsync(CancellationToken.None);

        service.LastUpdate.Should().NotBeNull();
        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues.Should().ContainKey("employeeType").WhoseValue.Should().Be(EmployeeType.Temporary);
    }

    [Fact]
    public async Task SuccessfulQuickEditRedirectPreservesCombinedFilters()
    {
        var service = new RecordingEmployeeService();
        var model = CreateAdministratorModel(service);
        model.Search = "MATCH";
        model.EmployeeType = EmployeeType.Temporary;
        model.QuickEdit = CreateQuickEdit(service.TemporaryEmployee);

        var result = await model.OnPostQuickEditAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues.Should().ContainKey("employeeType").WhoseValue.Should().Be(EmployeeType.Temporary);
        redirect.RouteValues.Should().ContainKey("search").WhoseValue.Should().Be("MATCH");
    }

    [Fact]
    public async Task QuickEditValidationErrorReloadPreservesCombinedFilters()
    {
        var service = new RecordingEmployeeService
        {
            UpdateException = new ArgumentException("Employee validation failed.")
        };
        var model = CreateAdministratorModel(service);
        model.Search = "MATCH";
        model.EmployeeType = EmployeeType.Temporary;
        model.QuickEdit = CreateQuickEdit(service.TemporaryEmployee);

        var result = await model.OnPostQuickEditAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        model.QuickEditOpen.Should().BeTrue();
        model.ModelState.IsValid.Should().BeFalse();
        service.LastSearch.Should().Be("MATCH");
        model.Employees.Should().ContainSingle()
            .Which.EmployeeType.Should().Be(EmployeeType.Temporary);
    }

    [Fact]
    public void EmployeeIndexOffersAllEmployeeTypeLabelsAndSearchInAGetFilter()
    {
        var razor = ReadPage("Employees", "Index.cshtml");

        razor.Should().Contain("<form method=\"get\"")
            .And.Contain("asp-for=\"Search\"")
            .And.Contain("asp-for=\"EmployeeType\"")
            .And.Contain(">全部<")
            .And.Contain(">正式员工<")
            .And.Contain(">劳务员工<")
            .And.Contain(">特殊临时人员<");
    }

    [Fact]
    public void EmployeeQuickEditPostCarriesCurrentFilters()
    {
        var razor = ReadPage("Employees", "Index.cshtml");

        razor.Should().Contain("name=\"Search\" value=\"@Model.Search\"")
            .And.Contain("name=\"EmployeeType\" value=\"@Model.EmployeeType\"");
    }

    private static void AssertOptionalGetBinding(string propertyName, Type propertyType)
    {
        var property = typeof(IndexModel).GetProperty(propertyName);

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(propertyType);
        var binding = property.GetCustomAttribute<BindPropertyAttribute>();
        binding.Should().NotBeNull();
        binding!.SupportsGet.Should().BeTrue();
    }

    private static IndexModel CreateAdministratorModel(RecordingEmployeeService service)
    {
        var identity = new ClaimsIdentity("EmployeeIndexTest", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "employee-index-test-user"));
        identity.AddClaim(new Claim(ClaimTypes.Role, SystemRoles.SystemAdministrator));
        return new IndexModel(service)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private static IndexModel.QuickEditInput CreateQuickEdit(EmployeeDto employee) => new()
    {
        Id = employee.Id,
        EmployeeNumber = employee.EmployeeNumber,
        Name = employee.Name,
        EmployeeType = employee.EmployeeType,
        IsActive = employee.IsActive,
        ConcurrencyStamp = employee.ConcurrencyStamp,
        Reason = "Test filtered quick edit"
    };

    private static string ReadPage(params string[] parts)
    {
        var path = Path.Combine(new[] { RepositoryRoot(), "src", "EngineeringManager.Web", "Pages" }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }

    private sealed class RecordingEmployeeService : IEmployeeService
    {
        private readonly IReadOnlyList<EmployeeDto> Items =
        [
            CreateEmployee("MATCH-FORMAL", EmployeeType.Formal),
            CreateEmployee("LABOR-001", EmployeeType.Labor),
            CreateEmployee("MATCH-TEMP", EmployeeType.Temporary)
        ];

        public EmployeeDto TemporaryEmployee => Items.Single(employee => employee.EmployeeType == EmployeeType.Temporary);
        public string? LastSearch { get; private set; }
        public UpdateEmployeeRequest? LastUpdate { get; private set; }
        public Exception? UpdateException { get; init; }

        public Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, CancellationToken cancellationToken)
        {
            LastSearch = search;
            var results = string.IsNullOrWhiteSpace(search)
                ? Items
                : Items.Where(employee =>
                    employee.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    employee.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
            return Task.FromResult<IReadOnlyList<EmployeeDto>>(results);
        }

        public Task<EmployeeDto> UpdateAsync(string userId, UpdateEmployeeRequest request, CancellationToken cancellationToken)
        {
            if (UpdateException is not null)
            {
                return Task.FromException<EmployeeDto>(UpdateException);
            }

            LastUpdate = request;
            return Task.FromResult(TemporaryEmployee);
        }

        public Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(employee => employee.Id == employeeId));

        public Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto> CopyAsync(CopyEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeAffiliationDto> AddAffiliationAsync(CreateEmployeeAffiliationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static EmployeeDto CreateEmployee(string number, EmployeeType employeeType) =>
            new(Guid.NewGuid(), number, number, employeeType, null, null, null, null, null, null, null, true, [], ConcurrencyStamp: Guid.NewGuid());
    }
}
