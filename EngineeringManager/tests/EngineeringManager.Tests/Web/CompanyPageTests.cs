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
        html.Should().Contain("data-company-scope-switcher");
        html.Should().Contain("全部公司");
        html.Should().Contain("公司数量");
        html.Should().Contain("未收款");
        html.Should().Contain("测试自有公司");
        html.Should().Contain("新增公司");
        html.Should().Contain("组合分类维护");
        html.Should().NotContain(">合同金额</span>");
        html.Should().NotContain(">账户余额</span>");
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

    [Fact]
    public async Task AdministratorSeesCompanyQuickEditAndDetailedEdit()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=profile"));

        html.Should().Contain("快捷编辑公司");
        html.Should().Contain("进入详细编辑");
        html.Should().Contain("data-inline-edit=\"company-details\"");
        html.Should().Contain("data-inline-cell-edit");
        html.Should().Contain("data-inline-edit-control");
        html.Should().NotContain("data-quick-edit-dialog");
    }

    [Fact]
    public async Task AdministratorSeesCompanyAccountNotesInputAndDetails()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts"));

        html.Should().Contain("name=\"Account.Notes\"");
        html.Should().Contain("账户备注");
    }

    [Fact]
    public async Task CompanyListShowsAccountCountAndDetailsProvideAccountManagement()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var listHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Companies"));
        var detailsHtml = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts"));

        listHtml.Should().Contain("data-column-key=\"accounts\"");
        detailsHtml.Should().Contain("data-company-account-table")
            .And.Contain("账户名称")
            .And.Contain("账号")
            .And.Contain("开户行")
            .And.Contain("账户类型")
            .And.Contain("期初余额")
            .And.Contain("默认用途")
            .And.Contain("账户备注")
            .And.Contain("编辑")
            .And.Contain("停用");
    }

    [Fact]
    public void CompanyAccountDtoCarriesConcurrencyStampForReliableEditing()
    {
        var root = RepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Application", "Companies", "CompanyDtos.cs"));

        source.Should().Contain("string? Notes = null,\n    Guid ConcurrencyStamp = default");
    }


    [Fact]
    public async Task AdministratorCompanyDetailsShowsTabsAndScopeSwitcher()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=overview"));
        html.Should().Contain("data-company-scope-switcher");
        html.Should().Contain("data-company-tabs");
        html.Should().Contain("经营概览");
        html.Should().Contain("基本信息");
        html.Should().Contain("证书信息");
        html.Should().Contain("账户信息");
        html.Should().Contain("项目与合同");
        html.Should().Contain("收付款与发票");
        html.Should().Contain("未收款");
        html.Should().Contain("未付款");
    }

    [Fact]
    public async Task AdministratorAccountsTabShowsEnableDisableLabelsNotDelete()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts"));
        html.Should().Contain("停用");
        html.Should().Contain("data-account-status-action");
        html.Should().NotContain("确认删除这个账户吗");
    }

    [Fact]
    public async Task FinanceCanOpenDetailsButNotManageAccounts()
    {
        await using var factory = CreateFactory("Finance");
        using var client = factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts"));
        html.Should().Contain("账户信息");
        html.Should().NotContain("快捷编辑公司");
        html.Should().NotContain("保存账户");
        html.Should().NotContain("data-account-status-action");
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
        public static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyListItemDto>>([new(CompanyId, "TEST", "测试自有公司", "测试公司", "一般纳税人有限公司", "测试法人", true, null, 1, 1)]);

        public Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanyDashboardDto(1, 1000m, 800m, 0m, 600m, 400m, 300m, 100m, 200m, 50m, 80m, 0m, 500m, DateTimeOffset.UtcNow));

        public Task<IReadOnlyList<CompanyCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CompanyCategoryDto>>([new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "GENERAL", "一般纳税人有限公司", 10, true, Guid.NewGuid())]);
        public Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken) => Task.FromResult(new CompanyDetailsDto(CompanyId, "TEST", "测试自有公司", "测试公司", Guid.Parse("22222222-2222-2222-2222-222222222222"), "一般纳税人有限公司", "测试法人", "913000000000000001", "注册地址", "经营地址", "13800000000", "测试开票抬头", null, true, Guid.NewGuid(), [new(Guid.NewGuid(), "基本户", null, null, "Bank", 0m, false, false, false, true, "账户备注")], []));
                public Task<CompanyWorkspaceSummaryDto> GetWorkspaceSummaryAsync(CompanyActor actor, Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanyWorkspaceSummaryDto(1, 1, 1, 1, 1, 1, 0));
        public Task<IReadOnlyList<CompanyActivityItemDto>> ListRecentActivityAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyActivityItemDto>>([new("collection", "测试收款", "摘要", 100m, new DateOnly(2026, 7, 20), CompanyId, Guid.NewGuid())]);
        public Task<IReadOnlyList<CompanyProjectRowDto>> ListCompanyProjectsAsync(CompanyActor actor, Guid companyId, string? search, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyProjectRowDto>>([new(CompanyId, "P-01", "测试项目", "InConstruction", 1000m, 600m, 400m, 300m, 100m)]);
        public Task<IReadOnlyList<CompanyContractRowDto>> ListCompanyContractsAsync(CompanyActor actor, Guid companyId, Guid? projectId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyContractRowDto>>([new(Guid.NewGuid(), CompanyId, "C-01", "测试合同", 1000m, 800m, 80m, true)]);
        public Task<IReadOnlyList<CompanyCollectionRowDto>> ListCompanyCollectionsAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyCollectionRowDto>>([new(Guid.NewGuid(), new DateOnly(2026, 7, 20), CompanyId, "P-01", "测试项目", "收款摘要", Guid.NewGuid(), "基本户", true, 400m)]);
        public Task<IReadOnlyList<CompanyPaymentRowDto>> ListCompanyPaymentsAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyPaymentRowDto>>([new(Guid.NewGuid(), new DateOnly(2026, 7, 21), CompanyId, "P-01", "测试项目", "付款摘要", Guid.NewGuid(), "基本户", true, 100m)]);
        public Task<IReadOnlyList<CompanyInvoiceRowDto>> ListCompanyInvoicesAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyInvoiceRowDto>>([new(Guid.NewGuid(), "销项", "INV-01", new DateOnly(2026, 7, 22), CompanyId, "P-01", "测试项目", "测试自有公司", 200m)]);
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

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
