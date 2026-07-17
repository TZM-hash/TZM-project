using System.Security.Claims;
using EngineeringManager.Application.Partners;
using EngineeringManager.Application.StageResults;
using EngineeringManager.Domain.Partners;
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

public sealed class PartnerStageResultAuthorizationTests
{
    [Fact]
    public async Task QueryUserCanReadPartnerAndStageResultLists()
    {
        await using var factory = CreateFactory("QueryOnly");
        using var client = factory.CreateClient();

        (await client.GetAsync("/Partners")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await client.GetAsync("/StageResults")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task SiteStaffCanCreateStageResultButCannotCreatePartner()
    {
        await using var factory = CreateFactory("SiteStaff");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/StageResults/Create")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await client.GetAsync("/Partners/Create")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProjectManagerCanCreatePartnerAndStageResult()
    {
        await using var factory = CreateFactory("ProjectManager");
        using var client = factory.CreateClient();

        (await client.GetAsync("/Partners/Create")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await client.GetAsync("/StageResults/Create")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    private static WebApplicationFactory<Program> CreateFactory(string role) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestHandler.Scheme;
                    options.DefaultChallengeScheme = TestHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, TestHandler>(TestHandler.Scheme, _ => { });
                services.RemoveAll<IBusinessPartnerService>();
                services.RemoveAll<IStageResultService>();
                services.AddSingleton<IBusinessPartnerService, FakePartnerService>();
                services.AddSingleton<IStageResultService, FakeStageResultService>();
            });
            builder.UseSetting(TestHandler.RoleSetting, role);
        });

    private sealed class FakePartnerService : IBusinessPartnerService
    {
        public Task<BusinessPartnerDto> CreateAsync(CreateBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto> CopyAsync(CopyBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto> UpdateAsync(string userId, UpdateBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LinkToProjectAsync(LinkPartnerToProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<BusinessPartnerDto>> ListAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BusinessPartnerDto>>([]);
        public Task<BusinessPartnerDto?> GetAsync(Guid partnerId, CancellationToken cancellationToken) => Task.FromResult<BusinessPartnerDto?>(null);
    }

    private sealed class FakeStageResultService : IStageResultService
    {
        public Task<StageResultDto> CreateAsync(CreateStageResultRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<StageResultDto>> ListByProjectAsync(Guid? projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StageResultDto>>([]);
        public Task<StageResultDto?> GetAsync(Guid stageResultId, CancellationToken cancellationToken) => Task.FromResult<StageResultDto?>(null);
    }

    private sealed class TestHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "PartnerStageTest";
        public const string RoleSetting = "PartnerStageTest:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "partner-stage-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "合作单位阶段成果测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role!));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
