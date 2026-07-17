using System.Net;
using System.Security.Claims;
using EngineeringManager.Application.Settings;
using EngineeringManager.Domain.Security;
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

public sealed class SystemSettingsPageTests
{
    [Fact]
    public async Task SystemAdministratorCanEditAllConfirmedDisplaySettings()
    {
        await using var factory = CreateFactory(SystemRoles.SystemAdministrator);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/Admin/Settings");

        html.Should().Contain("显示与交互设置");
        html.Should().Contain("name=\"Input.Theme\"").And.Contain("value=\"ClearGlass\"");
        html.Should().Contain("name=\"Input.Motion\"").And.Contain("value=\"Apple\"");
        html.Should().Contain("name=\"Input.Effects\"").And.Contain("value=\"High\"");
        html.Should().Contain("name=\"Input.Font\"").And.Contain("value=\"MicrosoftYaHei\"");
        html.Should().Contain("name=\"Input.Density\"").And.Contain("value=\"Compact\"");
        html.Should().Contain("type=\"submit\"").And.NotContain("data-settings-readonly");
    }

    [Fact]
    public async Task ApplicationAdministratorCanViewButCannotSaveSettings()
    {
        await using var factory = CreateFactory(SystemRoles.ApplicationAdministrator);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/Admin/Settings");

        html.Should().Contain("data-settings-readonly");
        html.Should().NotContain("type=\"submit\"");
    }

    [Fact]
    public async Task QueryOnlyUserCannotOpenSettings()
    {
        await using var factory = CreateFactory(SystemRoles.QueryOnly);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Admin/Settings");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static WebApplicationFactory<Program> CreateFactory(string role) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(SettingsAuthHandler.RoleSetting, role);
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = SettingsAuthHandler.Scheme;
                        options.DefaultChallengeScheme = SettingsAuthHandler.Scheme;
                        options.DefaultForbidScheme = SettingsAuthHandler.Scheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, SettingsAuthHandler>(SettingsAuthHandler.Scheme, _ => { });
                services.RemoveAll<ISystemSettingsService>();
                services.AddSingleton<ISystemSettingsService, FakeSettingsService>();
            });
        });

    private sealed class FakeSettingsService : ISystemSettingsService
    {
        public Task<SystemDisplaySettings> GetAsync(CancellationToken token) => Task.FromResult(SystemDisplaySettings.Default);
        public Task SaveAsync(SettingsActor actor, SystemDisplaySettings settings, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class SettingsAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "SettingsTest";
        public const string RoleSetting = "SettingsTest:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "settings-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "设置测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role!));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
