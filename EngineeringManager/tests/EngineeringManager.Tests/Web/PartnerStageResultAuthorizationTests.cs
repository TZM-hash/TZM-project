using System.Security.Claims;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Partners;
using EngineeringManager.Application.StageResults;
using EngineeringManager.Domain.Finance;
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
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/Partners")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await client.GetAsync("/StageResults")).StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
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

    [Fact]
    public async Task ProjectManagerSeesPartnerWorkspaceManagementDialogs()
    {
        await using var factory = CreateFactory("ProjectManager");
        using var client = factory.CreateClient();

        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/Partners"));

        html.Should().Contain("data-partner-dialog-open=\"edit\"")
            .And.Contain("data-partner-dialog-open=\"copy\"")
            .And.Contain("data-partner-editor-dialog")
            .And.NotContain("data-partner-financial-summary")
            .And.NotContain("快捷编辑合作单位")
            .And.NotContain("data-inline-cell-edit");
        factory.Services.GetRequiredService<RecordingCentralLedgerQueryService>().SummaryCallCount.Should().Be(0);
    }

    [Fact]
    public async Task QueryUserSeesPartnerDetailsWithoutManagementOrFinanceActions()
    {
        await using var factory = CreateFactory("QueryOnly");
        using var client = factory.CreateClient();

        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/Partners"));

        html.Should().Contain("data-partner-dialog-open=\"details\"")
            .And.Contain("data-partner-financial-summary")
            .And.NotContain("data-partner-dialog-open=\"edit\"")
            .And.NotContain("data-partner-dialog-open=\"copy\"")
            .And.NotContain("data-partner-finance-link");
    }

    [Fact]
    public async Task SystemAdministratorCanRenderManagementAndFinanceActionsTogether()
    {
        await using var factory = CreateFactory("SystemAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Partners");
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        html.Should().Contain("data-partner-dialog-open=\"edit\"")
            .And.Contain("data-partner-dialog-open=\"copy\"")
            .And.Contain("data-partner-dialog-open=\"finance\"")
            .And.Contain("data-partner-financial-summary")
            .And.Contain("data-partner-finance-dialog")
            .And.Contain("data-partner-finance-jump")
            .And.Contain("data-partner-finance-link");
    }

    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    [InlineData("Finance")]
    [InlineData("QueryOnly")]
    public async Task AuthorizedFinanceReadersSeePartnerFinancialSummary(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/Partners"));
        var overview = html[..html.IndexOf("<dialog", StringComparison.Ordinal)];

        overview.Should().Contain("data-partner-financial-summary")
            .And.Contain("应付")
            .And.Contain("已付")
            .And.Contain("未付")
            .And.Contain("销项票")
            .And.Contain("应开票")
            .And.Contain("已开票")
            .And.Contain("未开票")
            .And.Contain("role=\"progressbar\"")
            .And.Contain("data-progress-state=\"no-target\"")
            .And.Contain("aria-valuetext=\"—")
            .And.NotContain("data-column-key=\"receipts\"");
        factory.Services.GetRequiredService<RecordingCentralLedgerQueryService>().SummaryCallCount.Should().Be(1);
    }

    [Fact]
    public async Task PartnerFinancialSummaryRendersZeroTargetOverageAsFullOverProgress()
    {
        await using var factory = CreateFactory("Finance", zeroTargetOverage: true);
        using var client = factory.CreateClient();

        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/Partners"));

        html.Should().Contain("data-progress-state=\"over\"")
            .And.Contain("aria-valuetext=\"超额，已付 125.00，应付 0.00\" value=\"1\" max=\"1\"");
    }

    private static WebApplicationFactory<Program> CreateFactory(string role, bool zeroTargetOverage = false) =>
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
                services.RemoveAll<ICentralLedgerQueryService>();
                services.AddSingleton<IBusinessPartnerService, FakePartnerService>();
                services.AddSingleton<IStageResultService, FakeStageResultService>();
                services.AddSingleton(new RecordingCentralLedgerQueryService(zeroTargetOverage));
                services.AddSingleton<ICentralLedgerQueryService>(provider => provider.GetRequiredService<RecordingCentralLedgerQueryService>());
            });
            builder.UseSetting(TestHandler.RoleSetting, role);
        });

    private sealed class FakePartnerService : IBusinessPartnerService
    {
        public Task<BusinessPartnerDto> CreateAsync(CreateBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto> CopyAsync(CopyBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto> UpdateAsync(string userId, UpdateBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LinkToProjectAsync(LinkPartnerToProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        private static readonly BusinessPartnerDto Partner = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "PARTNER-001",
            "测试合作单位有限公司",
            "测试单位",
            "91330000TEST000001",
            "测试备注",
            [new PartnerRoleDto(BusinessPartnerRoleType.MaterialSupplier, "钢材", null, null)],
            [new PartnerContactDto(Guid.Parse("22222222-2222-2222-2222-222222222222"), "张三", "13800000000", null, null, true)],
            2,
            true,
            Guid.Parse("33333333-3333-3333-3333-333333333333"));

        public Task<IReadOnlyList<BusinessPartnerDto>> ListAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BusinessPartnerDto>>([Partner]);
        public Task<BusinessPartnerDto?> GetAsync(Guid partnerId, CancellationToken cancellationToken) => Task.FromResult<BusinessPartnerDto?>(partnerId == Partner.Id ? Partner : null);
    }

    private sealed class FakeStageResultService : IStageResultService
    {
        public Task<StageResultDto> CreateAsync(CreateStageResultRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<StageResultDto>> ListByProjectAsync(Guid? projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StageResultDto>>([]);
        public Task<StageResultDto?> GetAsync(Guid stageResultId, CancellationToken cancellationToken) => Task.FromResult<StageResultDto?>(null);
    }

    private sealed class RecordingCentralLedgerQueryService : ICentralLedgerQueryService
    {
        private readonly bool _zeroTargetOverage;

        public RecordingCentralLedgerQueryService(bool zeroTargetOverage = false) =>
            _zeroTargetOverage = zeroTargetOverage;

        public int SummaryCallCount { get; private set; }

        public Task<IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto>> GetPartnerSummariesAsync(
            CentralLedgerActor actor,
            IReadOnlyCollection<Guid> businessPartnerIds,
            CancellationToken token)
        {
            SummaryCallCount++;
            return Task.FromResult<IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto>>(
                businessPartnerIds.Distinct().ToDictionary(
                    id => id,
                    id => _zeroTargetOverage
                        ? new PartnerLedgerSummaryDto(id, CentralLedgerMetrics.Zero, CentralLedgerMetrics.Zero with { CashAmount = 125m })
                        : PartnerLedgerSummaryDto.Empty(id)));
        }

        public Task<CentralLedgerOverviewPageDto> SearchAsync(CentralLedgerActor actor, CentralLedgerQuery query, CancellationToken token) => throw new NotSupportedException();
        public Task<CentralLedgerDetailsDto?> GetAsync(CentralLedgerActor actor, FinanceRecordType type, Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<CentralLedgerOptionsDto> GetOptionsAsync(CentralLedgerActor actor, LedgerScope scope, CancellationToken token) => throw new NotSupportedException();
        public Task<CentralLedgerMetrics> GetProjectMetricsAsync(CentralLedgerActor actor, Guid projectId, CancellationToken token) => throw new NotSupportedException();
        public Task<CentralLedgerMetrics> GetPartnerMetricsAsync(CentralLedgerActor actor, Guid businessPartnerId, CancellationToken token) => throw new NotSupportedException();
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
