using System.Security.Claims;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Partners;
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

    [Fact]
    public async Task ProjectManagerSeesWorkspaceTabsActivityAndEditEntrances()
    {
        await using var factory = CreateFactory("ProjectManager");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/Projects/Details/{FakeProjectWorkspaceService.ProjectId}");
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        html.Should().Contain("工程量明细");
        html.Should().Contain("收款明细");
        html.Should().Contain("开票明细");
        html.Should().Contain("付款明细");
        html.Should().Contain("施工详情");
        html.Should().Contain("提示与记录");
        html.Should().Contain("他方挂靠我方");
        html.Should().Contain("快捷编辑");
        html.Should().Contain("进入详细编辑");
        html.Should().Contain("data-inline-edit=\"project-overview\"");
        html.Should().Contain("data-inline-edit=\"project-quantity\"");
        html.Should().Contain("data-inline-cell-edit");
        html.Should().Contain("data-inline-edit-control");
        html.Should().Contain("总包单位");
        html.Should().Contain("总包联系人");
        html.Should().Contain("项目负责人");
        html.Should().Contain("实际开工日期");
        html.Should().Contain("实际完工日期");
        html.Should().Contain("收款比例");
        html.Should().Contain("开票比例");
        html.Should().Contain("付款比例");
        html.Should().Contain("项目里程碑");
        html.Should().Contain("节点备注");
        html.Should().Contain("项目人员");
        html.Should().Contain("人员备注");
        html.Should().Contain("项目合作单位");
        html.Should().Contain("合作备注");
        html.Should().Contain("data-project-finance-edit");
        html.Should().Contain("data-inline-edit=\"project-collection\"");
        html.Should().Contain("data-inline-edit=\"project-invoice\"");
        html.Should().Contain("data-inline-edit=\"project-payment\"");
        html.Should().NotContain("data-quick-edit-dialog");
    }

    [Fact]
    public void ProjectOverviewRemovesDeprecatedFieldsAndFinanceTabsUseCellEditing()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml"));

        page.Should().NotContain("<dt>上级项目</dt>")
            .And.NotContain("<dt>分支机构</dt>")
            .And.Contain("data-inline-edit=\"project-collection\" data-inline-cell-edit")
            .And.Contain("data-inline-edit=\"project-invoice\" data-inline-cell-edit")
            .And.Contain("data-inline-edit=\"project-payment\" data-inline-cell-edit")
            .And.NotContain("data-inline-entry-edit");
    }

    [Fact]
    public async Task QueryOnlySeesWorkspaceWithoutEditEntrances()
    {
        await using var factory = CreateFactory("QueryOnly");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/Projects/Details/{FakeProjectWorkspaceService.ProjectId}");
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        html.Should().Contain("工程量明细");
        html.Should().Contain("提示与记录");
        html.Should().NotContain("快捷编辑");
        html.Should().NotContain("data-inline-edit-form");
        html.Should().NotContain("登记收款");
    }

    [Fact]
    public async Task ProjectListIncludesAffiliationFilter()
    {
        await using var factory = CreateFactory("ProjectManager");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/Projects");
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        html.Should().Contain("name=\"AffiliationType\"");
        html.Should().Contain("他方挂靠我方");
        html.Should().Contain("我方挂靠他方");
        html.Should().Contain("应收款");
        html.Should().Contain("未付款");
        html.Should().Contain("收款率");
        html.Should().Contain("开票率");
        html.Should().Contain("付款率");
        html.Should().Contain("mini-progress");
        html.Should().NotContain("Preliminary");
        html.Should().NotContain("Estimated");
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
                services.RemoveAll<IProjectWorkspaceService>();
                services.AddSingleton<IProjectWorkspaceService, FakeProjectWorkspaceService>();
                services.RemoveAll<IFinanceLedgerService>();
                services.AddSingleton<IFinanceLedgerService, FakeFinanceLedgerService>();
                services.RemoveAll<IProjectConstructionService>();
                services.AddSingleton<IProjectConstructionService, FakeProjectConstructionService>();
                services.RemoveAll<ISavedDataViewService>();
                services.AddSingleton<ISavedDataViewService, EmptySavedViewService>();
            });
            builder.UseSetting(ProjectTestAuthenticationHandler.RoleSetting, role ?? string.Empty);
        });

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }

    private sealed class FakeProjectService : IProjectService
    {
        public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractDto> AddContractAsync(CreateContractRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractLineItemDto> AddLineItemAsync(CreateContractLineItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractLineItemDto> UpdateLineItemAsync(UpdateContractLineItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectListItemDto>> ListProjectsAsync(string? search, ProjectStage? stage, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectListItemDto>>([]);
        public Task<ProjectListPageDto> SearchProjectsAsync(ProjectListActor actor, ProjectListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectListPageDto(
                [new ProjectListItemDto(
                    new ProjectDto(FakeProjectWorkspaceService.ProjectId, "P-WEB-001", "项目工作台页面测试", "测试总包单位", ProjectStage.UnderConstruction, ArchiveStatus.NotArchived, ProjectAffiliationType.ExternalPartyAttachedToUs),
                    new ProjectSummaryDto(300m, 200m, 0m, 200m, ProjectSettlementStatus.Estimated, 1, 1))],
                new ProjectListAggregateDto(1, 300m, 200m, 0), 1, 20, 1, 1, [FakeProjectWorkspaceService.ProjectId]));
        public Task<ProjectListOptionsDto> GetListOptionsAsync(ProjectListActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectListOptionsDto([], []));
        public Task<ProjectDetailsDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ProjectDetailsDto?>(null);
    }

    private sealed class EmptySavedViewService : ISavedDataViewService
    {
        public Task<IReadOnlyList<SavedDataViewDto>> ListAsync(string userId, DataViewDefinition definition, CancellationToken token) => Task.FromResult<IReadOnlyList<SavedDataViewDto>>([]);
        public Task<SavedDataViewDto> SaveAsync(string userId, SaveDataViewRequest request, DataViewDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(string userId, Guid id, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class FakeProjectWorkspaceService : IProjectWorkspaceService
    {
        public static readonly Guid ProjectId = Guid.Parse("71000000-0000-0000-0000-000000000001");

        public Task<ProjectWorkspaceDto?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectWorkspaceDto?>(projectId != ProjectId ? null : new ProjectWorkspaceDto(
                new ProjectWorkspaceOverviewDto(
                    ProjectId,
                    "P-WEB-001",
                    "项目工作台页面测试",
                    "上级项目",
                    "测试总包单位",
                    "测试联系人",
                    "13800000000",
                    null,
                    "项目负责人",
                    null,
                    "项目部",
                    null,
                    "一分公司",
                    ProjectStage.UnderConstruction,
                    ProjectAffiliationType.ExternalPartyAttachedToUs,
                    ArchiveStatus.NotArchived,
                    [new ProjectWorkspaceOptionDto(Guid.NewGuid().ToString(), "测试签约公司")],
                    new DateTimeOffset(2026, 7, 17, 8, 0, 0, TimeSpan.Zero),
                    Guid.Parse("71000000-0000-0000-0000-000000000002")),
                new ProjectSummaryDto(300m, 200m, 0m, 200m, ProjectSettlementStatus.Estimated, 1, 1),
                new FinanceProjectSummaryDto(ProjectId, 100m, 40m, 60m, 80m, 25m, 0m, 55m, 30m, 70m, 0m, false, false),
                [new ContractDto(Guid.NewGuid(), "C-WEB-001", "测试合同", ContractType.MainContract, ContractAllocationMode.SingleCompany, 300m,
                    [new ContractLineItemDto(Guid.NewGuid(), "001", "土方工程", "m³", 10m, 20m, 200m, null, null, 0m, false, Guid.NewGuid())])],
                [],
                [],
                [],
                [],
                [],
                [new ProjectActivityItemDto(new DateTimeOffset(2026, 7, 17, 8, 30, 0, TimeSpan.Zero), "修改记录", "编辑项目资料", "页面测试记录", "项目管理员", "normal")],
                [new ProjectMilestoneDto(Guid.NewGuid(), "阶段验收", new DateOnly(2026, 8, 1), null, false, 10, "节点备注")],
                [new ProjectAssignmentDto(Guid.NewGuid(), "member", "项目成员", ProjectAssignmentType.SiteStaff, true, "人员备注")],
                [new ProjectPartnerLinkDto(Guid.NewGuid(), Guid.NewGuid(), "施工班组", BusinessPartnerRoleType.ConstructionCrew, null, null, true, true, "合作备注")]));

        public Task<ProjectEditOptionsDto> GetEditOptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectEditOptionsDto([], [], [], []));

        public Task<ProjectWorkspaceDto> UpdateAsync(ProjectWorkspaceActor actor, UpdateProjectRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeFinanceLedgerService : IFinanceLedgerService
    {
        public Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectFinanceListItemDto>>([
                new(FakeProjectWorkspaceService.ProjectId, "P-WEB-001", "项目工作台页面测试", new FinanceProjectSummaryDto(FakeProjectWorkspaceService.ProjectId, 100m, 40m, 60m, 80m, 25m, 0m, 55m, 30m, 70m, 0m, false, false))
            ]);
        public Task<Guid> CreateAccountAsync(CreateFinancialAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FinancialAccountDto>>([]);
        public Task<FinanceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceOverviewPageDto> SearchOverviewAsync(FinanceOverviewQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceEntryOptionsDto> GetEntryOptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new FinanceEntryOptionsDto([], [], [], [], [], [], [], [], []));
        public Task<Guid> AddReceivableAsync(CreateReceivableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordCollectionAsync(RecordCollectionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordRefundAsync(RecordRefundRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddPayableAsync(CreatePayableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordPaymentReversalAsync(RecordPaymentReversalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> TransferAsync(CreateAccountTransferRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateReceivableAsync(FinanceRecordActor actor, UpdateReceivableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateCollectionAsync(FinanceRecordActor actor, UpdateCollectionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateInvoiceAsync(FinanceRecordActor actor, UpdateInvoiceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdatePayableAsync(FinanceRecordActor actor, UpdatePayableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdatePaymentAsync(FinanceRecordActor actor, UpdatePaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceProjectSummaryDto> GetSummaryAsync(FinanceSummaryFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceProjectSummaryDto> GetProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProjectConstructionService : IProjectConstructionService
    {
        public Task<ProjectConstructionWorkspaceDto> GetWorkspaceAsync(Guid projectId, DateOnly today, CancellationToken token) =>
            Task.FromResult(new ProjectConstructionWorkspaceDto([], [], [], [new ProjectConstructionOptionDto(FakeProjectWorkspaceService.ProjectId, "P-WEB-001 · 项目工作台页面测试")]));
        public Task<ProjectConstructionRecordDto> SaveAsync(ProjectConstructionActor actor, SaveProjectConstructionRecordRequest request, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectConstructionOptionDto> CreateEquipmentAsync(ProjectConstructionActor actor, CreateProjectEquipmentRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectConstructionOptionDto> CreateCrewAsync(ProjectConstructionActor actor, CreateProjectCrewRequest request, CancellationToken token) => throw new NotSupportedException();
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
