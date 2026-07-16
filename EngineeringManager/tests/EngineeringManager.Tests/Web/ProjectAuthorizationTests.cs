using System.Security.Claims;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectAuthorizationTests
{
    [Fact]
    public async Task AnonymousUserCannotOpenProjectList()
    {
        await using var factory = CreateFactory(null);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Projects");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("ProjectManager")]
    [InlineData("QueryOnly")]
    [InlineData("SiteStaff")]
    public async Task AuthorizedRolesCanReadProjectList(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/Projects");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("QueryOnly")]
    [InlineData("SiteStaff")]
    public async Task ReadOnlyRolesCannotOpenContractEdit(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Projects/Contracts/Edit");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    private static WebApplicationFactory<Program> CreateFactory(string? role) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = ProjectTestAuthenticationHandler.Scheme;
                    options.DefaultChallengeScheme = ProjectTestAuthenticationHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, ProjectTestAuthenticationHandler>(
                    ProjectTestAuthenticationHandler.Scheme,
                    _ => { });
                services.RemoveAll<IProjectService>();
                services.AddSingleton<IProjectService, FakeProjectService>();
            });
            builder.UseSetting(ProjectTestAuthenticationHandler.RoleSetting, role ?? string.Empty);
        });

    private sealed class FakeProjectService : IProjectService
    {
        public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractDto> AddContractAsync(CreateContractRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractLineItemDto> AddLineItemAsync(CreateContractLineItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectListItemDto>> ListProjectsAsync(string? search, ProjectStage? stage, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectListItemDto>>([]);
        public Task<ProjectDetailsDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ProjectDetailsDto?>(null);
    }

    private sealed class ProjectTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "ProjectTest";
        public const string RoleSetting = "ProjectTestAuth:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "project-test-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "项目测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
