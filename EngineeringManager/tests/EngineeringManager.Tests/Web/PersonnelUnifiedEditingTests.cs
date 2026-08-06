using System.Reflection;
using System.Security.Claims;
using EngineeringManager.Application.Personnel;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Personnel;
using EngineeringManager.Web.Pages.Personnel;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Tests.Web;

public sealed class PersonnelUnifiedEditingTests
{
    [Fact]
    public async Task UnifiedDetailsPostsPublicDataAndCurrentAffiliationCommands()
    {
        var personId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var concurrencyStamp = Guid.NewGuid();
        var service = new RecordingPersonnelService(Person(personId, CurrentAffiliation(
            PersonnelScope.External,
            null,
            ExternalPersonnelType.BusinessPartner,
            null,
            partnerId,
            concurrencyStamp)));

        var publicModel = new DetailsModel(service)
        {
            PersonId = personId,
            PageContext = AdministratorPageContext()
        };
        SetInput(publicModel, "PublicInput", new Dictionary<string, object?>
        {
            ["Name"] = "更新姓名",
            ["Phone"] = "13900000000",
            ["IdentityNumber"] = "330100199001010011",
            ["BankAccountNumber"] = "6222000011112222",
            ["BankName"] = "测试银行",
            ["Notes"] = "统一主档修改",
            ["IsActive"] = true,
            ["ConcurrencyStamp"] = service.Person.ConcurrencyStamp,
            ["Reason"] = "更新公共资料"
        });
        publicModel.ModelState.AddModelError("AffiliationInput.OwnerKey", "另一张表单未提交");
        publicModel.ModelState.AddModelError("SwitchInput.ConcurrencyStamp", "另一张表单未提交");

        var publicResult = await InvokeHandlerAsync(publicModel, "OnPostSavePublicDataAsync");

        publicResult.Should().BeOfType<RedirectToPageResult>();
        service.LastSavePersonRequest.Should().Be(new SavePersonRequest(
            personId,
            "更新姓名",
            "13900000000",
            "330100199001010011",
            "6222000011112222",
            "测试银行",
            "统一主档修改",
            true,
            service.Person.ConcurrencyStamp,
            "更新公共资料"));

        var affiliationModel = new DetailsModel(service)
        {
            PersonId = personId,
            PageContext = AdministratorPageContext()
        };
        SetInput(affiliationModel, "AffiliationInput", new Dictionary<string, object?>
        {
            ["OwnerKey"] = $"partner:{partnerId}",
            ["OrganizationUnitId"] = departmentId,
            ["ProjectId"] = projectId,
            ["PositionTitle"] = "供应商驻场",
            ["EffectiveDate"] = new DateOnly(2026, 8, 7),
            ["Reason"] = "调整外部人员归属",
            ["ConcurrencyStamp"] = concurrencyStamp
        });
        affiliationModel.ModelState.AddModelError("PublicInput.Name", "另一张表单未提交");
        affiliationModel.ModelState.AddModelError("SwitchInput.ConcurrencyStamp", "另一张表单未提交");

        var affiliationResult = await InvokeHandlerAsync(affiliationModel, "OnPostSaveAffiliationAsync");

        affiliationResult.Should().BeOfType<RedirectToPageResult>();
        service.LastSaveAffiliationRequest.Should().Be(new SavePersonnelAffiliationRequest(
            personId,
            PersonnelScope.External,
            null,
            ExternalPersonnelType.BusinessPartner,
            null,
            partnerId,
            departmentId,
            projectId,
            null,
            "供应商驻场",
            new DateOnly(2026, 8, 7),
            "调整外部人员归属",
            concurrencyStamp));
    }

    [Fact]
    public async Task ScopeSwitchIgnoresValidationErrorsFromTheOtherTwoForms()
    {
        var personId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var concurrencyStamp = Guid.NewGuid();
        var service = new RecordingPersonnelService(Person(personId, CurrentAffiliation(
            PersonnelScope.External,
            null,
            ExternalPersonnelType.BusinessPartner,
            null,
            Guid.NewGuid(),
            concurrencyStamp)));
        var model = new DetailsModel(service)
        {
            PersonId = personId,
            PageContext = AdministratorPageContext()
        };
        SetInput(model, "SwitchInput", new Dictionary<string, object?>
        {
            ["Scope"] = PersonnelScope.Internal,
            ["InternalType"] = EmployeeType.Formal,
            ["OwnerKey"] = $"legal:{companyId}",
            ["EffectiveDate"] = DateOnly.FromDateTime(DateTime.Today),
            ["Reason"] = "转为内部人员",
            ["ConcurrencyStamp"] = concurrencyStamp
        });
        model.ModelState.AddModelError("PublicInput.Name", "另一张表单未提交");
        model.ModelState.AddModelError("AffiliationInput.OwnerKey", "另一张表单未提交");

        var result = await model.OnPostSwitchScopeAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        service.LastSwitchRequest.Should().NotBeNull();
        service.LastSwitchRequest!.LegalEntityId.Should().Be(companyId);
    }

    [Fact]
    public async Task PersonnelCreatePagePostsExternalCrewPersonThroughUnifiedService()
    {
        var crewId = Guid.NewGuid();
        var createdId = Guid.NewGuid();
        var service = new RecordingPersonnelService(Person(createdId, null));
        var pageType = typeof(DetailsModel).Assembly.GetType("EngineeringManager.Web.Pages.Personnel.CreateModel");
        pageType.Should().NotBeNull();
        var model = Activator.CreateInstance(pageType!, service).Should().BeAssignableTo<PageModel>().Subject;
        model.PageContext = AdministratorPageContext();
        SetInput(model, "Input", new Dictionary<string, object?>
        {
            ["PersonNumber"] = "RY-NEW-001",
            ["Name"] = "新建班组人员",
            ["Scope"] = PersonnelScope.External,
            ["ExternalType"] = ExternalPersonnelType.ConstructionCrew,
            ["Phone"] = "13800000000",
            ["OwnerKey"] = $"partner:{crewId}",
            ["CrewBusinessPartnerId"] = crewId,
            ["PositionTitle"] = "木工",
            ["EffectiveDate"] = new DateOnly(2026, 8, 7),
            ["Reason"] = "新增外部班组人员"
        });

        var result = await InvokeHandlerAsync(model, "OnPostAsync");

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.PageName.Should().Be("/Personnel/Details");
        redirect.RouteValues!["personId"].Should().Be(createdId);
        service.LastCreateRequest.Should().Be(new CreatePersonRequest(
            "RY-NEW-001",
            "新建班组人员",
            PersonnelScope.External,
            null,
            ExternalPersonnelType.ConstructionCrew,
            Phone: "13800000000",
            BusinessPartnerId: crewId,
            CrewBusinessPartnerId: crewId,
            PositionTitle: "木工",
            EffectiveDate: new DateOnly(2026, 8, 7),
            Reason: "新增外部班组人员"));
    }

    [Fact]
    public async Task HistoricalAsOfViewRejectsCurrentAffiliationEdits()
    {
        var personId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var concurrencyStamp = Guid.NewGuid();
        var service = new RecordingPersonnelService(Person(personId, CurrentAffiliation(
            PersonnelScope.External,
            null,
            ExternalPersonnelType.BusinessPartner,
            null,
            partnerId,
            concurrencyStamp)));
        var model = new DetailsModel(service)
        {
            PersonId = personId,
            AsOf = DateOnly.FromDateTime(DateTime.Today).AddDays(-1),
            PageContext = AdministratorPageContext()
        };
        SetInput(model, "AffiliationInput", new Dictionary<string, object?>
        {
            ["OwnerKey"] = $"partner:{partnerId}",
            ["EffectiveDate"] = DateOnly.FromDateTime(DateTime.Today),
            ["Reason"] = "历史页面误操作",
            ["ConcurrencyStamp"] = concurrencyStamp
        });

        var result = await model.OnPostSaveAffiliationAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        service.LastSaveAffiliationRequest.Should().BeNull();
        model.ModelState.Values.SelectMany(item => item.Errors).Should()
            .Contain(item => item.ErrorMessage.Contains("历史日期查看模式只读", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HistoricalAsOfViewRejectsScopeSwitches()
    {
        var personId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var concurrencyStamp = Guid.NewGuid();
        var service = new RecordingPersonnelService(Person(personId, CurrentAffiliation(
            PersonnelScope.External,
            null,
            ExternalPersonnelType.BusinessPartner,
            null,
            partnerId,
            concurrencyStamp)));
        var model = new DetailsModel(service)
        {
            PersonId = personId,
            AsOf = DateOnly.FromDateTime(DateTime.Today).AddDays(-1),
            PageContext = AdministratorPageContext()
        };
        SetInput(model, "SwitchInput", new Dictionary<string, object?>
        {
            ["Scope"] = PersonnelScope.Internal,
            ["InternalType"] = EmployeeType.Formal,
            ["OwnerKey"] = $"legal:{Guid.NewGuid()}",
            ["EffectiveDate"] = DateOnly.FromDateTime(DateTime.Today),
            ["Reason"] = "历史页面误切换",
            ["ConcurrencyStamp"] = concurrencyStamp
        });

        var result = await model.OnPostSwitchScopeAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        service.LastSwitchRequest.Should().BeNull();
        model.ModelState.Values.SelectMany(item => item.Errors).Should()
            .Contain(item => item.ErrorMessage.Contains("历史日期查看模式只读", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AffiliationSaveErrorReloadsTheOtherFormsWithFreshConcurrencyValues()
    {
        var personId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var affiliationStamp = Guid.NewGuid();
        var person = Person(personId, CurrentAffiliation(
            PersonnelScope.External,
            null,
            ExternalPersonnelType.BusinessPartner,
            null,
            partnerId,
            affiliationStamp));
        var service = new RecordingPersonnelService(person) { RejectAffiliationSave = true };
        var model = new DetailsModel(service)
        {
            PersonId = personId,
            PageContext = AdministratorPageContext()
        };
        SetInput(model, "AffiliationInput", new Dictionary<string, object?>
        {
            ["OwnerKey"] = $"partner:{partnerId}",
            ["EffectiveDate"] = DateOnly.FromDateTime(DateTime.Today),
            ["Reason"] = "触发错误",
            ["ConcurrencyStamp"] = affiliationStamp
        });

        var result = await model.OnPostSaveAffiliationAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        model.PublicInput.ConcurrencyStamp.Should().Be(person.ConcurrencyStamp);
        model.SwitchInput.ConcurrencyStamp.Should().Be(affiliationStamp);
    }

    [Fact]
    public void UnifiedPersonnelPagesExposeCreatePublicDataAndLinkedAffiliationEditors()
    {
        var internalPage = ReadPage("Personnel", "Internal", "Index.cshtml");
        var externalPage = ReadPage("Personnel", "External", "Index.cshtml");
        var detailsPage = ReadPage("Personnel", "Details.cshtml");
        var createPage = ReadPageIfExists("Personnel", "Create.cshtml");

        internalPage.Should().Contain("asp-page=\"/Personnel/Create\"")
            .And.Contain("asp-route-scope=\"Internal\"");
        externalPage.Should().Contain("asp-page=\"/Personnel/Create\"")
            .And.Contain("asp-route-scope=\"External\"");
        detailsPage.Should().Contain("asp-page-handler=\"SavePublicData\"")
            .And.Contain("asp-page-handler=\"SaveAffiliation\"")
            .And.Contain("Model.CanEditCurrentAffiliation")
            .And.Contain("name=\"AffiliationInput.OwnerKey\"")
            .And.Contain("name=\"AffiliationInput.OrganizationUnitId\"")
            .And.Contain("name=\"AffiliationInput.ProjectId\"")
            .And.Contain("name=\"AffiliationInput.CrewBusinessPartnerId\"")
            .And.Contain("data-personnel-affiliation")
            .And.Contain("personnel-affiliation.js");
        createPage.Should().Contain("data-personnel-affiliation")
            .And.Contain("asp-for=\"Input.PersonNumber\"")
            .And.Contain("asp-for=\"Input.Scope\"")
            .And.Contain("data-affiliation-internal-type")
            .And.Contain("data-affiliation-external-type")
            .And.Contain("asp-for=\"Input.EffectiveDate\" type=\"date\" max=\"@DateOnly.FromDateTime(DateTime.Today).ToString(\"yyyy-MM-dd\")\"")
            .And.Contain("personnel-affiliation.js");
    }

    private static async Task<IActionResult> InvokeHandlerAsync(object model, string methodName)
    {
        var method = model.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        method.Should().NotBeNull();
        var task = method!.Invoke(model, [CancellationToken.None]).Should().BeAssignableTo<Task<IActionResult>>().Subject;
        return await task;
    }

    private static void SetInput(object model, string propertyName, IReadOnlyDictionary<string, object?> values)
    {
        var property = model.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        property.Should().NotBeNull();
        var input = Activator.CreateInstance(property!.PropertyType);
        input.Should().NotBeNull();
        foreach (var (name, value) in values)
        {
            var inputProperty = property.PropertyType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            inputProperty.Should().NotBeNull($"{propertyName}.{name} must be bindable");
            inputProperty!.SetValue(input, value);
        }
        property.SetValue(model, input);
    }

    private static PageContext AdministratorPageContext()
    {
        var identity = new ClaimsIdentity("Test", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "personnel-edit-test"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "SystemAdministrator"));
        return new PageContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    private static PersonnelDetailsDto Person(Guid id, PersonnelAffiliationDto? current) => new(
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
        current,
        current is null ? [] : [current]);

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
            externalType == ExternalPersonnelType.ConstructionCrew ? businessPartnerId : null,
            null,
            null,
            new DateOnly(2026, 1, 1),
            null,
            true,
            null,
            concurrencyStamp);

    private static string ReadPage(params string[] parts)
    {
        var path = Path.Combine(new[] { RepositoryRoot(), "src", "EngineeringManager.Web", "Pages" }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }

    private static string ReadPageIfExists(params string[] parts)
    {
        var path = Path.Combine(new[] { RepositoryRoot(), "src", "EngineeringManager.Web", "Pages" }.Concat(parts).ToArray());
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }

    private sealed class RecordingPersonnelService(PersonnelDetailsDto person) : IPersonnelService
    {
        public PersonnelDetailsDto Person { get; } = person;
        public CreatePersonRequest? LastCreateRequest { get; private set; }
        public SavePersonRequest? LastSavePersonRequest { get; private set; }
        public SavePersonnelAffiliationRequest? LastSaveAffiliationRequest { get; private set; }
        public SwitchPersonnelScopeRequest? LastSwitchRequest { get; private set; }
        public bool RejectAffiliationSave { get; init; }

        public Task<PersonnelDetailsDto> CreateAsync(string userId, CreatePersonRequest request, CancellationToken cancellationToken)
        {
            LastCreateRequest = request;
            return Task.FromResult(Person);
        }

        public Task<PersonnelDetailsDto?> GetAsync(Guid personId, DateOnly? asOf, bool canViewSensitiveData, CancellationToken cancellationToken) =>
            Task.FromResult<PersonnelDetailsDto?>(Person.Id == personId ? Person : null);

        public Task<PersonnelDetailsDto> SavePublicDataAsync(string userId, SavePersonRequest request, CancellationToken cancellationToken)
        {
            LastSavePersonRequest = request;
            return Task.FromResult(Person);
        }

        public Task<PersonnelAffiliationDto> SaveAffiliationAsync(string userId, SavePersonnelAffiliationRequest request, CancellationToken cancellationToken)
        {
            if (RejectAffiliationSave) throw new InvalidOperationException("模拟归属保存失败");
            LastSaveAffiliationRequest = request;
            return Task.FromResult(Person.CurrentAffiliation!);
        }

        public Task<PersonnelOptionSetDto> GetOptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PersonnelOptionSetDto([], [], [], [], []));

        public Task<IReadOnlyList<PersonnelListItemDto>> ListAsync(PersonnelListQuery query, bool canViewSensitiveData, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersonnelDetailsDto> SwitchScopeAsync(string userId, SwitchPersonnelScopeRequest request, CancellationToken cancellationToken)
        {
            LastSwitchRequest = request;
            return Task.FromResult(Person);
        }
        public Task<Guid?> ResolvePersonIdForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
    }
}
