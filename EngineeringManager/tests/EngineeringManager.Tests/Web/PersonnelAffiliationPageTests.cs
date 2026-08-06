using System.Security.Claims;
using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Web.Pages.Employees;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Tests.Web;

public sealed class PersonnelAffiliationPageTests
{
    [Fact]
    public void YellowAffiliationAreaContainsFourLinkedSelectsAndDateEffectiveFields()
    {
        var page = ReadPage("Employees", "Details.cshtml");

        page.Should().Contain("data-personnel-affiliation")
            .And.Contain("asp-page-handler=\"SaveAffiliation\"")
            .And.Contain("name=\"AffiliationInput.OwnerKey\"")
            .And.Contain("name=\"AffiliationInput.OrganizationUnitId\"")
            .And.Contain("name=\"AffiliationInput.ProjectId\"")
            .And.Contain("name=\"AffiliationInput.CrewBusinessPartnerId\"")
            .And.Contain("name=\"AffiliationInput.EffectiveDate\"")
            .And.Contain("name=\"AffiliationInput.Reason\"")
            .And.Contain("data-affiliation-owner")
            .And.Contain("data-affiliation-department")
            .And.Contain("data-affiliation-project")
            .And.Contain("data-affiliation-crew")
            .And.Contain("data-legal-entity-id")
            .And.Contain("data-business-partner-id")
            .And.Contain("data-role=\"crew\"")
            .And.Contain("personnel-affiliation.js");
    }

    [Fact]
    public void AffiliationModuleFiltersDependentOptionsAndForcesCrewOwnership()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "personnel-affiliation.js");

        script.Should().Contain("selectedOwner.dataset.legalEntityId")
            .And.Contain("selectedOwner.dataset.businessPartnerId")
            .And.Contain("option.hidden = !matchesOwner")
            .And.Contain("departmentSelect.value = ''")
            .And.Contain("projectSelect.value = ''")
            .And.Contain("selectedOwner.dataset.role === 'crew'")
            .And.Contain("crewSelect.value = businessPartnerId")
            .And.Contain("legalEntityInput.value")
            .And.Contain("businessPartnerInput.value");
    }

    [Fact]
    public async Task SaveAffiliationUsesCurrentScopeAndPreservesTheEmployeeTab()
    {
        var employeeId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var crewId = Guid.NewGuid();
        var concurrencyStamp = Guid.NewGuid();
        var service = new RecordingPersonnelService(personId, CurrentAffiliation(
            PersonnelScope.Internal,
            EmployeeType.Labor,
            null,
            legalEntityId,
            null,
            concurrencyStamp));
        var businessYearId = Guid.NewGuid();
        var model = CreateModel(service, employeeId, businessYearId, "expenses");
        model.AffiliationInput = new DetailsModel.PersonnelAffiliationInput
        {
            OwnerKey = $"legal:{legalEntityId}",
            OrganizationUnitId = departmentId,
            ProjectId = projectId,
            CrewBusinessPartnerId = crewId,
            PositionTitle = "施工经理",
            EffectiveDate = new DateOnly(2026, 8, 7),
            Reason = "调整当前项目",
            ConcurrencyStamp = concurrencyStamp
        };

        var result = await model.OnPostSaveAffiliationAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues!["id"].Should().Be(employeeId);
        redirect.RouteValues["businessYearId"].Should().Be(businessYearId);
        redirect.RouteValues["tab"].Should().Be("expenses");
        service.LastSaveRequest.Should().Be(new SavePersonnelAffiliationRequest(
            personId,
            PersonnelScope.Internal,
            EmployeeType.Labor,
            null,
            legalEntityId,
            null,
            departmentId,
            projectId,
            crewId,
            "施工经理",
            new DateOnly(2026, 8, 7),
            "调整当前项目",
            concurrencyStamp));
    }

    [Fact]
    public async Task ExternalCrewOwnerIsAlsoForcedAsTheCurrentCrewOnTheServer()
    {
        var employeeId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var crewId = Guid.NewGuid();
        var otherCrewId = Guid.NewGuid();
        var concurrencyStamp = Guid.NewGuid();
        var service = new RecordingPersonnelService(personId, CurrentAffiliation(
            PersonnelScope.External,
            null,
            ExternalPersonnelType.ConstructionCrew,
            null,
            crewId,
            concurrencyStamp));
        var model = CreateModel(service, employeeId, null, "wages");
        model.AffiliationInput = new DetailsModel.PersonnelAffiliationInput
        {
            OwnerKey = $"partner:{crewId}",
            CrewBusinessPartnerId = otherCrewId,
            EffectiveDate = new DateOnly(2026, 8, 7),
            Reason = "施工班组归属变更",
            ConcurrencyStamp = concurrencyStamp
        };

        await model.OnPostSaveAffiliationAsync(CancellationToken.None);

        service.LastSaveRequest!.BusinessPartnerId.Should().Be(crewId);
        service.LastSaveRequest.CrewBusinessPartnerId.Should().Be(crewId);
    }

    private static DetailsModel CreateModel(RecordingPersonnelService personnelService, Guid employeeId, Guid? businessYearId, string tab) =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, personnelService)
        {
            Id = employeeId,
            BusinessYearId = businessYearId,
            Tab = tab,
            PageContext = PageContextForAdministrator()
        };

    private static PageContext PageContextForAdministrator()
    {
        var identity = new ClaimsIdentity("Test", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "affiliation-test"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "SystemAdministrator"));
        return new PageContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    private static PersonnelAffiliationDto CurrentAffiliation(
        PersonnelScope scope,
        EmployeeType? internalType,
        ExternalPersonnelType? externalType,
        Guid? legalEntityId,
        Guid? businessPartnerId,
        Guid concurrencyStamp) => new(
            Guid.NewGuid(),
            scope,
            internalType,
            externalType,
            legalEntityId,
            null,
            businessPartnerId,
            null,
            null,
            null,
            null,
            null,
            businessPartnerId,
            null,
            null,
            new DateOnly(2026, 1, 1),
            null,
            true,
            null,
            concurrencyStamp);

    private static string ReadPage(params string[] parts)
    {
        var pathParts = new List<string> { "src", "EngineeringManager.Web", "Pages" };
        pathParts.AddRange(parts);
        return ReadFile(pathParts.ToArray());
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }

    private sealed class RecordingPersonnelService(Guid personId, PersonnelAffiliationDto currentAffiliation) : IPersonnelService
    {
        public SavePersonnelAffiliationRequest? LastSaveRequest { get; private set; }

        public Task<Guid?> ResolvePersonIdForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(personId);

        public Task<PersonnelDetailsDto?> GetAsync(Guid requestedPersonId, DateOnly? asOf, bool canViewSensitiveData, CancellationToken cancellationToken) =>
            Task.FromResult<PersonnelDetailsDto?>(new PersonnelDetailsDto(
                personId,
                "RY0001",
                "测试人员",
                null,
                null,
                null,
                null,
                null,
                true,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                currentAffiliation,
                [currentAffiliation]));

        public Task<PersonnelAffiliationDto> SaveAffiliationAsync(string userId, SavePersonnelAffiliationRequest request, CancellationToken cancellationToken)
        {
            LastSaveRequest = request;
            return Task.FromResult(currentAffiliation);
        }

        public Task<PersonnelOptionSetDto> GetOptionsAsync(CancellationToken cancellationToken) => Task.FromResult(new PersonnelOptionSetDto([], [], [], [], []));
        public Task<PersonnelDetailsDto> CreateAsync(string userId, CreatePersonRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PersonnelListItemDto>> ListAsync(PersonnelListQuery query, bool canViewSensitiveData, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelDetailsDto> SavePublicDataAsync(string userId, SavePersonRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelDetailsDto> SwitchScopeAsync(string userId, SwitchPersonnelScopeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
