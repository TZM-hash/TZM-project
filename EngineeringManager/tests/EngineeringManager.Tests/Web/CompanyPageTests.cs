using System.Net;
using System.Security.Claims;
using EngineeringManager.Application.Companies;
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

public sealed class CompanyPageTests
{
    [Fact]
    public async Task AnonymousUserIsRedirectedFromCompanies()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Companies");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AdministratorSeesCompanyDashboardAndDirectAmounts()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Companies"));

        html.Should().Contain("data-company-dashboard");
        html.Should().Contain("data-company-money-chart");
        html.Should().Contain("1,000.00");
        html.Should().Contain("测试自有公司");
        html.Should().Contain("新增公司");
        html.Should().Contain("组合分类维护");
    }

    [Fact]
    public async Task FinanceCanReadButCannotEditCompanies()
    {
        await using var factory = CreateFactory("Finance");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var list = await client.GetAsync("/Companies");
        using var edit = await client.GetAsync("/Companies/Edit");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        edit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static WebApplicationFactory<Program> CreateFactory(string role) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(TestAuthHandler.RoleSetting, role);
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
                services.RemoveAll<ICompanyManagementService>();
                services.AddSingleton<ICompanyManagementService, FakeCompanyService>();
                services.RemoveAll<ICompanyActorService>();
                services.AddSingleton<ICompanyActorService, FakeCompanyActorService>();
            });
        });

    private sealed class FakeCompanyActorService : ICompanyActorService
    {
        public Task<CompanyActor> ResolveAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanyActor(userId, roles.Contains("ApplicationAdministrator"), true, []));
    }

    private sealed class FakeCompanyService : ICompanyManagementService
    {
        private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyListItemDto>>([new(CompanyId, "TEST", "测试自有公司", "测试公司", "一般纳税人有限公司", "测试法人", true)]);

        public Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanyDashboardDto(1, 1000m, 800m, 0m, 600m, 400m, 300m, 100m, 200m, 50m, 80m, 0m, 500m, DateTimeOffset.UtcNow));

        public Task<IReadOnlyList<CompanyCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CompanyCategoryDto>>([]);
        public Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyDetailsDto> SaveCompanyAsync(CompanyActor actor, SaveCompanyRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SaveCompanyRequest> PrepareCopyAsync(CompanyActor actor, Guid sourceId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyCategoryDto> SaveCategoryAsync(CompanyActor actor, SaveCompanyCategoryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyAccountDto> SaveAccountAsync(CompanyActor actor, SaveCompanyAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyCertificateDto> SaveCertificateAsync(CompanyActor actor, SaveCompanyCertificateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "CompanyTest";
        public const string RoleSetting = "CompanyTest:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "company-test-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "公司测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role!));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
