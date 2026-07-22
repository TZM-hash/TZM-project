using System.Reflection;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Web.Pages.Employees;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

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
    [InlineData("PageNumber", typeof(int))]
    [InlineData("PageSize", typeof(int))]
    public void PaginationInputsAreGetBindings(string propertyName, Type propertyType)
    {
        AssertOptionalGetBinding(propertyName, propertyType);
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
    public void EmployeeIndexOffersAllEmployeeTypeLabelsInSharedInlineFilter()
    {
        var razor = ReadPage("Employees", "Index.cshtml");

        razor.Should().Contain("_DataWorkbench")
            .And.Contain("InlineFilters")
            .And.Contain("new(\"Search\"")
            .And.Contain("new(\"EmployeeType\"")
            .And.Contain("正式员工")
            .And.Contain("劳务员工")
            .And.Contain("特殊临时人员");
    }

    [Fact]
    public void EmployeeListUsesProjectStyleWorkspaceWithoutRowQuickEdit()
    {
        var razor = ReadPage("Employees", "Index.cshtml");

        razor.Should().Contain("人员经营台账")
            .And.Contain("年度总账")
            .And.Contain("证书总览")
            .And.Contain("当前业务年度应付总额")
            .And.Contain("data-column-key=\"current_company\"")
            .And.Contain("data-column-key=\"payable\"")
            .And.Contain("data-column-key=\"paid\"")
            .And.Contain("data-column-key=\"unpaid\"")
            .And.Contain(">查看</a>")
            .And.Contain("详细编辑")
            .And.NotContain("data-inline-cell-edit")
            .And.NotContain("QuickEdit");
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
            => Task.FromResult(TemporaryEmployee);

        public Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(employee => employee.Id == employeeId));

        public Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto> CopyAsync(CopyEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeAffiliationDto> AddAffiliationAsync(CreateEmployeeAffiliationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static EmployeeDto CreateEmployee(string number, EmployeeType employeeType) =>
            new(Guid.NewGuid(), number, number, employeeType, null, null, null, null, null, null, null, true, [], ConcurrencyStamp: Guid.NewGuid());
    }
}
