using System.Security.Claims;
using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using InternalIndexModel = EngineeringManager.Web.Pages.Personnel.Internal.IndexModel;
using ExternalIndexModel = EngineeringManager.Web.Pages.Personnel.External.IndexModel;
using PersonnelDetailsModel = EngineeringManager.Web.Pages.Personnel.DetailsModel;

namespace EngineeringManager.Tests.Web;

public sealed class PersonnelPageTests
{
    [Fact]
    public void MainNavigationExposesOnlyTheTwoPersonnelScopes()
    {
        var layout = ReadPage("Shared", "_Layout.cshtml");

        layout.Should().Contain("人员管理")
            .And.Contain("asp-page=\"/Personnel/Internal/Index\"")
            .And.Contain("asp-page=\"/Personnel/External/Index\"")
            .And.Contain("内部人员")
            .And.Contain("外部人员")
            .And.NotContain("<a class='@Nav(\"/Employees\")' asp-page=\"/Employees/Index\">员工业务档案</a>");
    }

    [Fact]
    public void PersonnelCategoryTabsDoNotPresentEmployeeBusinessRecordsAsAThirdScope()
    {
        var pages = new[]
        {
            ReadPage("Personnel", "Internal", "Index.cshtml"),
            ReadPage("Personnel", "External", "Index.cshtml"),
            ReadPage("Personnel", "Details.cshtml"),
            ReadPage("Personnel", "Create.cshtml")
        };

        pages.Should().OnlyContain(page => !page.Contains(
            "<a class=\"button button--secondary button--small\" asp-page=\"/Employees/Index\">员工业务档案</a>",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task InternalWorkbenchPassesEveryGetFilterToPersonnelService()
    {
        var service = new RecordingPersonnelService();
        var legalEntityId = Guid.NewGuid();
        var crewBusinessPartnerId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var asOf = new DateOnly(2026, 8, 6);
        var model = new InternalIndexModel(service)
        {
            Search = "张三",
            LegalEntityId = legalEntityId,
            CrewBusinessPartnerId = crewBusinessPartnerId,
            DepartmentId = departmentId,
            InternalType = EmployeeType.Labor,
            IsActive = false,
            AsOf = asOf,
            PageContext = PageContextForViewer()
        };

        await model.OnGetAsync(CancellationToken.None);

        service.LastListQuery.Should().Be(new PersonnelListQuery(
            PersonnelScope.Internal,
            "张三",
            legalEntityId,
            null,
            departmentId,
            EmployeeType.Labor,
            null,
            false,
            asOf,
            crewBusinessPartnerId));
        service.LastCanViewSensitiveData.Should().BeTrue();
    }

    [Fact]
    public async Task ExternalWorkbenchPassesEveryGetFilterToPersonnelService()
    {
        var service = new RecordingPersonnelService();
        var businessPartnerId = Guid.NewGuid();
        var crewBusinessPartnerId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var asOf = new DateOnly(2026, 8, 6);
        var model = new ExternalIndexModel(service)
        {
            Search = "班组人员",
            BusinessPartnerId = businessPartnerId,
            CrewBusinessPartnerId = crewBusinessPartnerId,
            DepartmentId = departmentId,
            ExternalType = ExternalPersonnelType.ConstructionCrew,
            IsActive = true,
            AsOf = asOf,
            PageContext = PageContextForViewer()
        };

        await model.OnGetAsync(CancellationToken.None);

        service.LastListQuery.Should().Be(new PersonnelListQuery(
            PersonnelScope.External,
            "班组人员",
            null,
            businessPartnerId,
            departmentId,
            null,
            ExternalPersonnelType.ConstructionCrew,
            true,
            asOf,
            crewBusinessPartnerId));
        service.LastCanViewSensitiveData.Should().BeTrue();
    }

    [Fact]
    public async Task UnifiedDetailsLoadsPersonAndAvailableAffiliationOptions()
    {
        var personId = Guid.NewGuid();
        var service = new RecordingPersonnelService(personId);
        var model = new PersonnelDetailsModel(service)
        {
            PersonId = personId,
            AsOf = new DateOnly(2026, 8, 6),
            PageContext = PageContextForViewer()
        };

        var result = await model.OnGetAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        model.Person.Id.Should().Be(personId);
        service.LastGetPersonId.Should().Be(personId);
        service.OptionsReadCount.Should().Be(1);
    }

    [Fact]
    public async Task ScopeSwitchPostsACompleteDateEffectiveRequest()
    {
        var personId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var concurrencyStamp = Guid.NewGuid();
        var service = new RecordingPersonnelService(personId);
        var model = new PersonnelDetailsModel(service)
        {
            PersonId = personId,
            PageContext = PageContextForAdministrator(),
            SwitchInput = new PersonnelDetailsModel.ScopeSwitchInput
            {
                Scope = PersonnelScope.External,
                InternalType = EmployeeType.Formal,
                ExternalType = ExternalPersonnelType.ConstructionCrew,
                BusinessPartnerId = partnerId,
                CrewBusinessPartnerId = partnerId,
                EffectiveDate = new DateOnly(2026, 8, 7),
                PositionTitle = "班组长",
                Reason = "转为外部班组人员",
                ConcurrencyStamp = concurrencyStamp
            }
        };

        var result = await model.OnPostSwitchScopeAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        service.LastSwitchRequest.Should().Be(new SwitchPersonnelScopeRequest(
            personId,
            PersonnelScope.External,
            null,
            ExternalPersonnelType.ConstructionCrew,
            null,
            partnerId,
            null,
            null,
            partnerId,
            "班组长",
            new DateOnly(2026, 8, 7),
            "转为外部班组人员",
            concurrencyStamp));
    }

    [Fact]
    public void WorkbenchMarkupUsesRealQueryFieldsAndUnifiedDetailsLinks()
    {
        var internalPage = ReadPage("Personnel", "Internal", "Index.cshtml");
        var externalPage = ReadPage("Personnel", "External", "Index.cshtml");
        var detailsPage = ReadPage("Personnel", "Details.cshtml");

        foreach (var page in new[] { internalPage, externalPage })
        {
            page.Should().Contain("name=\"Search\"")
                .And.Contain("name=\"CrewBusinessPartnerId\"")
                .And.Contain("name=\"DepartmentId\"")
                .And.Contain("name=\"IsActive\"")
                .And.Contain("asp-page=\"/Personnel/Details\"")
                .And.Contain("asp-route-personId");
        }

        internalPage.Should().Contain("name=\"InternalType\"")
            .And.Contain("name=\"LegalEntityId\"")
            .And.NotContain("name=\"BusinessPartnerId\"")
            .And.Contain("asp-page=\"/Employees/Ledger\"")
            .And.Contain("asp-page=\"/Employees/Certificates/Index\"");
        externalPage.Should().Contain("name=\"ExternalType\"")
            .And.Contain("name=\"BusinessPartnerId\"")
            .And.NotContain("name=\"LegalEntityId\"")
            .And.Contain("asp-page=\"/Crews/Details\"")
            .And.Contain("asp-page=\"/Partners/Details\"");
        detailsPage.Should().Contain("asp-page-handler=\"SwitchScope\"")
            .And.Contain("SwitchInput.EffectiveDate")
            .And.Contain("SwitchInput.Reason")
            .And.Contain("SwitchInput.ConcurrencyStamp")
            .And.Contain("归属历史")
            .And.Contain("asp-page=\"/Employees/Details\"")
            .And.Contain("asp-page=\"/Crews/Details\"");
    }

    [Fact]
    public void PersonnelWorkspacesUseSharedWorkbenchWithSelectableSalaryColumnsAndExport()
    {
        var pages = new[]
        {
            ReadPage("Personnel", "Internal", "Index.cshtml"),
            ReadPage("Personnel", "External", "Index.cshtml")
        };
        var models = new[]
        {
            ReadSourceFile("src", "EngineeringManager.Web", "Pages", "Personnel", "Internal", "Index.cshtml.cs"),
            ReadSourceFile("src", "EngineeringManager.Web", "Pages", "Personnel", "External", "Index.cshtml.cs")
        };

        pages.Should().OnlyContain(page => page.Contains("_DataWorkbench", StringComparison.Ordinal))
            .And.OnlyContain(page => page.Contains("data-personnel-export-item", StringComparison.Ordinal))
            .And.OnlyContain(page => page.Contains("data-column-key=\"current_year_new_payable\"", StringComparison.Ordinal))
            .And.OnlyContain(page => page.Contains("data-column-key=\"current_year_unpaid\"", StringComparison.Ordinal))
            .And.OnlyContain(page => page.Contains("data-column-key=\"received_amount\"", StringComparison.Ordinal));
        models.Should().OnlyContain(model => model.Contains("OnPostExportAsync", StringComparison.Ordinal))
            .And.OnlyContain(model => model.Contains("EmployeeAnnualLedgerSummary", StringComparison.Ordinal))
            .And.OnlyContain(model => model.Contains("ISavedDataViewService", StringComparison.Ordinal));
    }

    private static PageContext PageContextForViewer() => PageContextWithRoles("Finance");

    private static PageContext PageContextForAdministrator() => PageContextWithRoles("SystemAdministrator");

    private static PageContext PageContextWithRoles(params string[] roles)
    {
        var identity = new ClaimsIdentity("Test", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "personnel-test"));
        foreach (var role in roles) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new PageContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    private static string ReadPage(params string[] parts)
    {
        var path = Path.Combine(new[] { RepositoryRoot(), "src", "EngineeringManager.Web", "Pages" }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }

    private static string ReadSourceFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }

    private sealed class RecordingPersonnelService(Guid? personId = null) : IPersonnelService
    {
        private readonly Guid personId = personId ?? Guid.NewGuid();

        public PersonnelListQuery? LastListQuery { get; private set; }
        public bool LastCanViewSensitiveData { get; private set; }
        public Guid? LastGetPersonId { get; private set; }
        public SwitchPersonnelScopeRequest? LastSwitchRequest { get; private set; }
        public int OptionsReadCount { get; private set; }

        public Task<IReadOnlyList<PersonnelListItemDto>> ListAsync(PersonnelListQuery query, bool canViewSensitiveData, CancellationToken cancellationToken)
        {
            LastListQuery = query;
            LastCanViewSensitiveData = canViewSensitiveData;
            return Task.FromResult<IReadOnlyList<PersonnelListItemDto>>([]);
        }

        public Task<PersonnelDetailsDto?> GetAsync(Guid requestedPersonId, DateOnly? asOf, bool canViewSensitiveData, CancellationToken cancellationToken)
        {
            LastGetPersonId = requestedPersonId;
            return Task.FromResult<PersonnelDetailsDto?>(requestedPersonId == personId ? Person(requestedPersonId) : null);
        }

        public Task<PersonnelOptionSetDto> GetOptionsAsync(CancellationToken cancellationToken)
        {
            OptionsReadCount++;
            return Task.FromResult(new PersonnelOptionSetDto([], [], [], [], []));
        }

        public Task<PersonnelDetailsDto> SwitchScopeAsync(string userId, SwitchPersonnelScopeRequest request, CancellationToken cancellationToken)
        {
            LastSwitchRequest = request;
            return Task.FromResult(Person(request.PersonId));
        }

        public Task<PersonnelDetailsDto> CreateAsync(string userId, CreatePersonRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelDetailsDto> SavePublicDataAsync(string userId, SavePersonRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelAffiliationDto> SaveAffiliationAsync(string userId, SavePersonnelAffiliationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid?> ResolvePersonIdForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);

        private static PersonnelDetailsDto Person(Guid id) => new(
            id,
            "RY0001",
            "测试人员",
            "13800000000",
            null,
            null,
            null,
            null,
            true,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            []);
    }
}
