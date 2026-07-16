using System.Security.Claims;
using EngineeringManager.Application.Offline;
using EngineeringManager.Domain.Offline;
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

public sealed class OfflineStageResultAuthorizationTests
{
    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    [InlineData("ProjectManager")]
    [InlineData("SiteStaff")]
    public async Task FieldRolesCanOpenOfflineStageResultPage(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        (await client.GetAsync("/StageResults/Offline")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Finance")]
    [InlineData("QueryOnly")]
    public async Task NonFieldRolesCannotOpenOfflineStageResultPage(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/StageResults/Offline")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnonymousUserIsRedirectedToLogin()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/StageResults/Offline")).StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
    }

    private static WebApplicationFactory<Program> CreateFactory(string role) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = OfflineTestHandler.Scheme;
                    options.DefaultChallengeScheme = OfflineTestHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, OfflineTestHandler>(OfflineTestHandler.Scheme, _ => { });
                services.RemoveAll<IOfflineStageResultService>();
                services.AddSingleton<IOfflineStageResultService, FakeOfflineService>();
            });
            builder.UseSetting(OfflineTestHandler.RoleSetting, role);
        });

    private sealed class FakeOfflineService : IOfflineStageResultService
    {
        public Task<IReadOnlyList<OfflineProjectOptionDto>> GetProjectOptionsAsync(OfflineSyncActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OfflineProjectOptionDto>>([]);
        public Task<OfflineDraftSyncResultDto> SyncDraftAsync(OfflineSyncActor actor, OfflineDraftSyncRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new OfflineDraftSyncResultDto(OfflineSyncStatus.Synced, Guid.NewGuid(), Guid.NewGuid(), false, null, null));
        public Task<OfflinePhotoSyncResultDto> SyncPhotoAsync(OfflineSyncActor actor, OfflinePhotoSyncRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new OfflinePhotoSyncResultDto(Guid.NewGuid(), false));
        public Task ReportFailureAsync(OfflineSyncActor actor, Guid clientDraftId, string errorMessage, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class OfflineTestHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "OfflineTest";
        public const string RoleSetting = "OfflineTestAuth:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "offline-web-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "离线测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role!));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
