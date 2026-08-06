using EngineeringManager.Application.Organization;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Web.Pages.Organization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Tests.Web;

public sealed class OrganizationDepartmentPageTests
{
    [Fact]
    public async Task LegalEntityQueryLoadsOnlyThatOrganizationsDepartments()
    {
        var ownerId = Guid.NewGuid();
        var service = new RecordingOrganizationService();
        var model = new DepartmentsModel(service) { LegalEntityId = ownerId };

        var result = await model.OnGetAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        service.LastOwnerKind.Should().Be(OrganizationOwnerKind.LegalEntity);
        service.LastOwnerId.Should().Be(ownerId);
        service.LastIncludeInactive.Should().BeTrue();
    }

    [Fact]
    public async Task OwnerQueryRequiresExactlyOneOrganizationIdentifier()
    {
        var model = new DepartmentsModel(new RecordingOrganizationService())
        {
            LegalEntityId = Guid.NewGuid(),
            BusinessPartnerId = Guid.NewGuid()
        };

        var result = await model.OnGetAsync(CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void DepartmentPageOffersCrudStatusAndPersonnelDrillDowns()
    {
        var page = ReadPage("Organization", "Departments.cshtml");

        page.Should().Contain("asp-page-handler=\"Save\"")
            .And.Contain("asp-page-handler=\"Deactivate\"")
            .And.Contain("重新启用")
            .And.Contain("当前人员")
            .And.Contain("asp-page=\"/Personnel/Internal/Index\"")
            .And.Contain("asp-page=\"/Personnel/External/Index\"")
            .And.Contain("asp-route-departmentId")
            .And.Contain("asp-route-legalEntityId")
            .And.Contain("asp-route-businessPartnerId");
    }

    [Fact]
    public void CompanyCrewAndPartnerDetailsExposeDepartmentSettings()
    {
        var company = ReadPage("Companies", "Details.cshtml");
        var crew = ReadPage("Crews", "Details.cshtml");
        var partner = ReadPage("Partners", "Details.cshtml");

        company.Should().Contain("部门设置")
            .And.Contain("asp-page=\"/Organization/Departments\"")
            .And.Contain("asp-route-legalEntityId=\"@Model.Company.Id\"");
        foreach (var page in new[] { crew, partner })
        {
            page.Should().Contain("部门设置")
                .And.Contain("asp-page=\"/Organization/Departments\"")
                .And.Contain("asp-route-businessPartnerId");
        }
    }

    [Fact]
    public void OrganizationSummaryDepartmentLinkReturnsCrewOwnersToCrewDetails()
    {
        var partial = ReadPage("Shared", "_OrganizationSummary.cshtml");

        partial.Should().Contain("summary.IsConstructionCrew ? \"/Crews/Details\" : \"/Partners/Details\"")
            .And.Contain("ReturnPage=")
            .And.Contain("Uri.EscapeDataString");
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

    private sealed class RecordingOrganizationService : IOrganizationService
    {
        public OrganizationOwnerKind? LastOwnerKind { get; private set; }
        public Guid? LastOwnerId { get; private set; }
        public bool LastIncludeInactive { get; private set; }

        public Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(OrganizationOwnerKind ownerKind, Guid ownerId, bool includeInactive, CancellationToken cancellationToken)
        {
            LastOwnerKind = ownerKind;
            LastOwnerId = ownerId;
            LastIncludeInactive = includeInactive;
            return Task.FromResult<IReadOnlyList<DepartmentDto>>([]);
        }

        public Task<DepartmentDto> SaveDepartmentAsync(SaveDepartmentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeactivateDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrganizationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) => Task.FromResult(new OrganizationOverviewDto([], []));
        public Task<OrganizationUnitDto> CreateOrganizationUnitAsync(CreateOrganizationUnitRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LegalEntityDto> CreateLegalEntityAsync(CreateLegalEntityRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
