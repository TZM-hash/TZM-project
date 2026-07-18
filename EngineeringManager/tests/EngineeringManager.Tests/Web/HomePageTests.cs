using System.Net;
using System.Security.Claims;
using EngineeringManager.Application.Dashboard;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EngineeringManager.Tests.Web;

public sealed class HomePageTests
{
    [Fact]
    public async Task HomePageUsesChineseResponsiveShellWithoutExternalCdn()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        var manifest = await client.GetStringAsync("/manifest.webmanifest");
        var serviceWorker = await client.GetStringAsync("/service-worker.js");
        var siteScript = await client.GetStringAsync("/js/site.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("工程项目经营管理系统");
        html.Should().Contain("name=\"viewport\"");
        html.Should().Contain("/css/base.");
        html.Should().NotContain("https://cdn.");
        html.Should().Contain("/manifest.");
        html.Should().Contain("阶段 0～10 已完成");
        html.Should().NotContain("阶段 7 开发中");
        html.Should().NotContain("阶段成果");
        manifest.Should().Contain("工程项目经营管理系统");
        serviceWorker.Should().Contain("engineering-manager-shell-v7");
        siteScript.Should().Contain("/service-worker.js");
    }

    [Fact]
    public async Task AnonymousHomeDoesNotExposeBusinessDashboard()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        html.Should().NotContain("data-business-dashboard");
        html.Should().Contain("登录后查看经营数据");
    }

    [Fact]
    public async Task LoginPageRendersWithoutServerError()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/Identity/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("内部账号登录");
        html.Should().NotContain("Register as a new user");
        html.Should().NotContain("jquery-validation");
    }

    [Fact]
    public async Task PublicRegistrationIsDisabled()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Identity/Account/Register");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthenticatedHomeShowsMetricsChartsRisksAndOfflineStatus()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = DashboardAuthHandler.Scheme;
                    options.DefaultChallengeScheme = DashboardAuthHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, DashboardAuthHandler>(DashboardAuthHandler.Scheme, _ => { });
                services.RemoveAll<IDashboardService>();
                services.AddSingleton<IDashboardService, FakeDashboardService>();
            }));
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        html.Should().Contain("data-business-dashboard");
        html.Should().Contain("data-stage-chart");
        html.Should().Contain("data-money-chart");
        html.Should().Contain("data-dashboard-risks");
        html.Should().Contain("data-dashboard-offline");
        html.Should().Contain("data-chart-series");
        html.Should().Contain("data-dashboard-equipment");
        html.Should().Contain("data-dashboard-payroll");
        html.Should().Contain("1,000.00");
    }

    private sealed class FakeDashboardService : IDashboardService
    {
        public Task<DashboardDto> GetAsync(DashboardActor actor, CancellationToken cancellationToken) => Task.FromResult(new DashboardDto(
            2,
            1000m,
            200m,
            1,
            true,
            true,
            [new DashboardStageDto(ProjectStage.UnderConstruction, "施工中", 2, 100m)],
            [new DashboardMoneyComparisonDto("receivable", "收款进度", 1000m, 600m, 400m, 60m)],
            [new DashboardRiskDto(Guid.NewGuid(), ReminderSeverity.Warning, "测试风险", "存在未收款", "Project", Guid.NewGuid().ToString())],
            DateTimeOffset.UtcNow));
    }

    private sealed class DashboardAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "DashboardAuth";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "dashboard-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "驾驶舱用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "SystemAdministrator"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
