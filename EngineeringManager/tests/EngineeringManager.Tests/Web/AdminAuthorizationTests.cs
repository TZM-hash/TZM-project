using System.Security.Claims;
using EngineeringManager.Application.Organization;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EngineeringManager.Tests.Web;

public sealed class AdminAuthorizationTests
{
    [Fact]
    public async Task AnonymousUserCannotOpenOrganizationManagement()
    {
        await using var factory = CreateFactory(null);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Admin/Organizations");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApplicationAdministratorCanOpenOrganizationManagement()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/Admin/Organizations");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task QueryOnlyUserCannotOpenOrganizationManagement()
    {
        await using var factory = CreateFactory("QueryOnly");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Admin/Organizations");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    private static WebApplicationFactory<Program> CreateFactory(string? role)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme,
                    _ => { });
                services.RemoveAll<IOrganizationService>();
                services.AddSingleton<IOrganizationService, FakeOrganizationService>();
            });
            builder.UseSetting(TestAuthenticationHandler.RoleSetting, role ?? string.Empty);
        });
    }

    private sealed class FakeOrganizationService : IOrganizationService
    {
        public Task<OrganizationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new OrganizationOverviewDto([], []));

        public Task<OrganizationUnitDto> CreateOrganizationUnitAsync(CreateOrganizationUnitRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LegalEntityDto> CreateLegalEntityAsync(CreateLegalEntityRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";
        public const string RoleSetting = "TestAuth:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "test-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
