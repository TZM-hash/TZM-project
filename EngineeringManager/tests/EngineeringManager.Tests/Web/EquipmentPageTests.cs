using System.Net;
using System.Security.Claims;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.Partners;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EngineeringManager.Tests.Web;

public sealed class EquipmentPageTests
{
    [Fact]
    public async Task AnonymousUserIsRedirectedFromEquipment()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/Equipment");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task EquipmentManagerSeesDashboardAndOfflineEntry()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Equipment"));
        html.Should().Contain("data-equipment-dashboard");
        html.Should().Contain("测试挖掘机");
        html.Should().Contain("现场离线记录");
    }

    [Fact]
    public async Task EquipmentManagerSeesQuickEditAndDropdownOptions()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Details/{FakeEquipmentService.EquipmentId}"));

        html.Should().Contain("快捷编辑设备");
        html.Should().Contain("EQ-COMP · 设备公司");
        html.Should().Contain("测试出租方");
        html.Should().Contain("进入详细编辑");
        html.Should().Contain("data-inline-edit=\"equipment-details\"");
        html.Should().Contain("data-inline-cell-edit");
        html.Should().Contain("data-inline-edit-control");
        html.Should().NotContain("data-quick-edit-dialog");
    }

    [Fact]
    public async Task EquipmentManagerSeesEquipmentAndSettlementNotesFields()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var details = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Details/{FakeEquipmentService.EquipmentId}"));
        var edit = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Edit?id={FakeEquipmentService.EquipmentId}"));
        var settlement = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Settlement?usageId={Guid.NewGuid()}"));

        details.Should().Contain("设备备注");
        details.Should().Contain("name=\"QuickEdit.Notes\"");
        edit.Should().Contain("name=\"Input.Notes\"");
        settlement.Should().Contain("name=\"Notes\"");
    }

    private static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting(AuthHandler.RoleSetting, "EquipmentManager");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = AuthHandler.Scheme; options.DefaultChallengeScheme = AuthHandler.Scheme; })
                .AddScheme<AuthenticationSchemeOptions, AuthHandler>(AuthHandler.Scheme, _ => { });
            services.RemoveAll<IEquipmentService>();
            services.AddSingleton<IEquipmentService, FakeEquipmentService>();
            services.RemoveAll<ICompanyManagementService>();
            services.AddSingleton<ICompanyManagementService, FakeCompanyService>();
            services.RemoveAll<IBusinessPartnerService>();
            services.AddSingleton<IBusinessPartnerService, FakePartnerService>();
        });
    });

    private sealed class FakeEquipmentService : IEquipmentService
    {
        public static readonly Guid EquipmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        public Task<EquipmentDashboardDto> GetDashboardAsync(EquipmentActor actor, EquipmentFilter filter, CancellationToken token) =>
            Task.FromResult(new EquipmentDashboardDto(1, 1, 0, 1, 1200m, new Dictionary<string, int> { ["InUse"] = 1 }, [new EquipmentDetailsDto(EquipmentId, "EQ-TEST", "测试挖掘机", "X1", "挖掘机", EquipmentOwnershipType.Rented, EquipmentStatus.InUse, null, FakePartnerService.PartnerId, 500m, Guid.NewGuid(), "设备备注")]));
        public Task<EquipmentDetailsDto> SaveEquipmentAsync(EquipmentActor actor, SaveEquipmentRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<EquipmentDetailsDto> CopyEquipmentAsync(EquipmentActor actor, Guid sourceId, CancellationToken token) => throw new NotSupportedException();
        public Task<EquipmentUsageDto> SaveUsageAsync(EquipmentActor actor, SaveEquipmentUsageRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task TransferOwnershipAsync(EquipmentActor actor, TransferEquipmentOwnershipRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<Guid> SaveMaintenanceAsync(EquipmentActor actor, SaveEquipmentMaintenanceRequest request, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class FakeCompanyService : ICompanyManagementService
    {
        public Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CompanyListItemDto>>([new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "EQ-COMP", "测试设备公司", "设备公司", "一般纳税人", "法人", true)]);
        public Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyDetailsDto> SaveCompanyAsync(CompanyActor actor, SaveCompanyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SaveCompanyRequest> PrepareCopyAsync(CompanyActor actor, Guid sourceId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompanyCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyCategoryDto> SaveCategoryAsync(CompanyActor actor, SaveCompanyCategoryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyAccountDto> SaveAccountAsync(CompanyActor actor, SaveCompanyAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyCertificateDto> SaveCertificateAsync(CompanyActor actor, SaveCompanyCertificateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakePartnerService : IBusinessPartnerService
    {
        public static readonly Guid PartnerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        public Task<IReadOnlyList<BusinessPartnerDto>> ListAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BusinessPartnerDto>>([new(PartnerId, "LESSOR", "测试出租方", "测试出租方", null, null, [new PartnerRoleDto(BusinessPartnerRoleType.MiscellaneousSupplier, null, null, null)], [], 0, true, Guid.NewGuid())]);
        public Task<BusinessPartnerDto> CreateAsync(CreateBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto> CopyAsync(CopyBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto> UpdateAsync(string userId, UpdateBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LinkToProjectAsync(LinkPartnerToProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto?> GetAsync(Guid partnerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "EquipmentTest";
        public const string RoleSetting = "EquipmentTest:Role";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting]!;
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "equipment-test-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "设备测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
