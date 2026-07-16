using System.Net;
using System.Security.Claims;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Domain.Equipment;
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

    private static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting(AuthHandler.RoleSetting, "EquipmentManager");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = AuthHandler.Scheme; options.DefaultChallengeScheme = AuthHandler.Scheme; })
                .AddScheme<AuthenticationSchemeOptions, AuthHandler>(AuthHandler.Scheme, _ => { });
            services.RemoveAll<IEquipmentService>();
            services.AddSingleton<IEquipmentService, FakeEquipmentService>();
        });
    });

    private sealed class FakeEquipmentService : IEquipmentService
    {
        public Task<EquipmentDashboardDto> GetDashboardAsync(EquipmentActor actor, EquipmentFilter filter, CancellationToken token) =>
            Task.FromResult(new EquipmentDashboardDto(1, 1, 0, 1, 1200m, new Dictionary<string, int> { ["InUse"] = 1 }, [new EquipmentDetailsDto(Guid.NewGuid(), "EQ-TEST", "测试挖掘机", "X1", "挖掘机", EquipmentOwnershipType.Rented, EquipmentStatus.InUse, null, Guid.NewGuid(), null, Guid.NewGuid())]));
        public Task<EquipmentDetailsDto> SaveEquipmentAsync(EquipmentActor actor, SaveEquipmentRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<EquipmentDetailsDto> CopyEquipmentAsync(EquipmentActor actor, Guid sourceId, CancellationToken token) => throw new NotSupportedException();
        public Task<EquipmentUsageDto> SaveUsageAsync(EquipmentActor actor, SaveEquipmentUsageRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task TransferOwnershipAsync(EquipmentActor actor, TransferEquipmentOwnershipRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<Guid> SaveMaintenanceAsync(EquipmentActor actor, SaveEquipmentMaintenanceRequest request, CancellationToken token) => throw new NotSupportedException();
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
