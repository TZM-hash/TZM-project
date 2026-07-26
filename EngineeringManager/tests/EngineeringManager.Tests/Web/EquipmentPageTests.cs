using System.Net;
using System.Security.Claims;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.EmployeeAnnualLedger;
using EngineeringManager.Application.Equipment;
using EngineeringManager.Application.Partners;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Web;
using EngineeringManager.Web.Pages.Equipment;
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
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment?OpenUsageEquipmentId={FakeEquipmentService.EquipmentId}"));
        html.Should().Contain("data-equipment-dashboard");
        html.Should().NotContain("data-equipment-company-filter");
        html.Should().Contain("name=\"CompanyId\"");
        html.Should().Contain("<option value=\"\">全部公司</option>");
        html.Should().Contain("data-equipment-workspace-summary");
        html.Should().Contain("data-equipment-workspace-list");
        html.Should().Contain("data-equipment-company-summary");
        html.Should().Contain("设备构成");
        html.Should().Contain("自有 0 · 租赁 1 · 其他 0");
        html.Should().Contain("data-equipment-editor-dialog");
        html.Should().Contain("data-equipment-details-dialog");
        html.Should().Contain("data-equipment-usage-dialog");
        html.Should().Contain("data-equipment-delete-dialog");
        html.Should().Contain("data-equipment-delete-open");
        html.Should().Contain("name=\"DeleteInput.ConfirmationNumber\"");
        html.Should().Contain("name=\"Editor.Status\"");
        html.Should().Contain("mac-window-dialog");
        html.Should().Contain("mac-window-controls");
        html.Should().Contain("data-attachment-preview-trigger");
        html.Should().Contain("data-attachment-preview-dialog");
        html.Should().Contain("data-equipment-usage-history");
        html.Should().Contain("data-equipment-usage-editor");
        html.Should().Contain("data-equipment-usage-edit");
        html.Should().Contain("2026 业务年");
        html.Should().Contain("name=\"Editor.ManagingLegalEntityId\"");
        html.Should().Contain("<option value=\"Other\">其他</option>");
        html.Should().Contain("enctype=\"multipart/form-data\"");
        html.Should().Contain("data-equipment-dialog-open=\"edit\"");
        html.Should().Contain("data-equipment-dialog-open=\"copy\"");
        html.Should().Contain("data-equipment-dialog-open=\"usage\"");
        html.Should().Contain("action-button--view");
        html.Should().Contain("action-button--edit");
        html.Should().Contain("action-button--copy");
        html.Should().Contain("action-button--usage");
        html.Should().Contain("<select required")
            .And.Contain("id=\"UsageInput_ProjectId\" name=\"UsageInput.ProjectId\"");
        html.Should().Contain("PRJ-001 · 测试项目");
        html.Should().NotContain("href=\"/Equipment/Edit");
        html.Should().Contain("测试挖掘机");
        html.Should().Contain("现场离线记录");
        html.Should().Contain("data-column-key=\"ownership_type\">设备归属</th>");
        html.Should().Contain("class=\"company-row-actions equipment-row-actions\"");
        html.Should().Contain("class=\"workbench-dialog company-create-dialog equipment-dialog");
        html.Should().NotContain("证书临期");
        html.Should().NotContain("证书过期");
        html.Should().NotContain("equipment-status-list");
        html.Should().NotContain("停用设备公司");
    }

    [Fact]
    public async Task EquipmentManagerSeesQuickEditAndDropdownOptions()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Details/{FakeEquipmentService.EquipmentId}"));

        html.Should().Contain("快捷编辑设备");
        html.Should().Contain("EQ-COMP · 设备公司");
        html.Should().Contain("测试出租方");
        html.Should().Contain("进入详细编辑");
        html.Should().Contain("data-inline-edit=\"equipment-details\"");
        html.Should().Contain("data-inline-cell-edit");
        html.Should().Contain("data-inline-edit-control");
        html.Should().NotContain("data-quick-edit-dialog");
    }

    [Fact]
    public async Task EquipmentManagerSeesEquipmentAndSettlementNotesFields()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var details = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Details/{FakeEquipmentService.EquipmentId}"));
        var edit = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Edit?id={FakeEquipmentService.EquipmentId}"));
        var settlement = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Settlement?usageId={Guid.NewGuid()}"));

        details.Should().Contain("设备备注");
        details.Should().Contain("name=\"QuickEdit.Notes\"");
        edit.Should().Contain("name=\"Input.Notes\"");
        edit.Should().Contain("name=\"Input.ManagingLegalEntityId\"");
        edit.Should().Contain("name=\"Input.PurchaseDate\"");
        edit.Should().Contain("name=\"Input.PurchaseAmount\"");
        edit.Should().Contain("name=\"Input.QualificationCertificateNumber\"");
        edit.Should().Contain("name=\"QualificationAttachmentFile\"");
        edit.Should().Contain("enctype=\"multipart/form-data\"");
        edit.Should().Contain("EQ-COMP · 设备公司");
        edit.Should().Contain("测试出租方");
        edit.Should().Contain("<option value=\"Other\">其他</option>");
        settlement.Should().Contain("name=\"Notes\"");
    }

    [Fact]
    public void ClosingAnExistingOpenUsageCreatesItsDefaultWorkPeriod()
    {
        var input = new UsageModel.InputModel
        {
            Id = Guid.NewGuid(),
            EquipmentId = FakeEquipmentService.EquipmentId,
            ProjectId = FakeProjectService.ProjectId,
            LegalEntityId = FakeCompanyService.CompanyId,
            EntryDate = new DateOnly(2026, 7, 1),
            ExitDate = new DateOnly(2026, 7, 5),
            Periods = []
        };

        var request = input.ToRequest();

        request.Periods.Should().ContainSingle(period => period.StartDate == input.EntryDate && period.EndDate == input.ExitDate && period.PeriodType == EquipmentPeriodType.Work);
    }

    [Fact]
    public async Task SiteStaffCannotSeeUsageMutationControls()
    {
        await using var factory = CreateFactory("SiteStaff");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Equipment"));

        html.Should().NotContain("data-equipment-dialog-open=\"usage\"");
        html.Should().NotContain("data-equipment-usage-create");
        html.Should().NotContain("data-equipment-usage-edit data-equipment-usage-payload");
    }

    [Fact]
    public async Task ReadOnlyEquipmentDetailsDoNotLinkToUsageRegistration()
    {
        await using var factory = CreateFactory("QueryOnly");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment/Details/{FakeEquipmentService.EquipmentId}"));

        html.Should().NotContain("登记进退场");
        html.Should().NotContain("/Equipment/Usage");
    }

    [Fact]
    public async Task SiteStaffCannotOpenLegacyUsageRegistration()
    {
        await using var factory = CreateFactory("SiteStaff");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync($"/Equipment/Usage?equipmentId={FakeEquipmentService.EquipmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InactiveCompanyFilterFallsBackToAllCompanies()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Equipment?CompanyId={FakeCompanyService.InactiveCompanyId}"));

        html.Should().Contain("全部公司 · 自有与租赁设备统一归类、证照和项目使用管理");
        html.Should().NotContain("停用设备公司");
    }

    [Fact]
    public async Task DefaultScopeRemainsAllCompaniesWhenUnassignedEquipmentExists()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Equipment"));

        html.Should().Contain("全部公司 · 自有与租赁设备统一归类、证照和项目使用管理");
        html.Should().Contain("<option value=\"\">全部公司</option>");
    }

    [Fact]
    public async Task OpenUsageDialogQueriesOnlyTheSelectedEquipment()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await client.GetAsync($"/Equipment?OpenUsageEquipmentId={FakeEquipmentService.EquipmentId}");

        var equipmentService = (FakeEquipmentService)factory.Services.GetRequiredService<IEquipmentService>();
        equipmentService.LastUsageFilter.Should().NotBeNull();
        equipmentService.LastUsageFilter!.EquipmentId.Should().Be(FakeEquipmentService.EquipmentId);
    }

    [Fact]
    public async Task EquipmentListDefersUsageHistoryUntilADialogIsOpened()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await client.GetAsync("/Equipment");

        var equipmentService = (FakeEquipmentService)factory.Services.GetRequiredService<IEquipmentService>();
        equipmentService.UsageQueryCount.Should().Be(0);
    }

    private static WebApplicationFactory<Program> CreateFactory(string role = "EquipmentManager") => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting(AuthHandler.RoleSetting, role);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = AuthHandler.Scheme; options.DefaultChallengeScheme = AuthHandler.Scheme; })
                .AddScheme<AuthenticationSchemeOptions, AuthHandler>(AuthHandler.Scheme, _ => { });
            services.RemoveAll<IEquipmentService>();
            services.AddSingleton<IEquipmentService, FakeEquipmentService>();
            services.RemoveAll<ICompanyManagementService>();
            services.AddSingleton<ICompanyManagementService, FakeCompanyService>();
            services.RemoveAll<IBusinessPartnerService>();
            services.AddSingleton<IBusinessPartnerService, FakePartnerService>();
            services.RemoveAll<IProjectService>();
            services.AddSingleton<IProjectService, FakeProjectService>();
            services.RemoveAll<IBusinessYearService>();
            services.AddSingleton<IBusinessYearService, FakeBusinessYearService>();
        });
    });

    private sealed class FakeEquipmentService : IEquipmentService
    {
        public static readonly Guid EquipmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        public EquipmentUsageFilter? LastUsageFilter { get; private set; }
        public int UsageQueryCount { get; private set; }
        public Task<EquipmentDetailsDto> GetEquipmentAsync(EquipmentActor actor, Guid id, CancellationToken token) =>
            Task.FromResult(CreateEquipment());
        public Task<EngineeringManager.Application.Certificates.CertificateFileDto> DownloadQualificationAttachmentAsync(EquipmentActor actor, Guid equipmentId, CancellationToken token) => throw new NotSupportedException();
        public Task<EquipmentDashboardDto> GetDashboardAsync(EquipmentActor actor, EquipmentFilter filter, CancellationToken token) =>
            Task.FromResult(new EquipmentDashboardDto(2, 1, 1, 1, 1200m, new Dictionary<string, int> { ["InUse"] = 1, ["Idle"] = 1 }, [CreateEquipment(), CreateEquipment() with
            {
                Id = Guid.Parse("34343434-3434-3434-3434-343434343434"),
                EquipmentNumber = "EQ-UNASSIGNED",
                Name = "待分配设备",
                Status = EquipmentStatus.Idle,
                ManagingLegalEntityId = null,
                ManagingLegalEntityName = null
            }]));
        public Task<EquipmentDetailsDto> SaveEquipmentAsync(EquipmentActor actor, SaveEquipmentRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteEquipmentAsync(EquipmentActor actor, Guid id, Guid concurrencyStamp, string confirmationNumber, string reason, CancellationToken token) => throw new NotSupportedException();
        public Task<EquipmentDetailsDto> CopyEquipmentAsync(EquipmentActor actor, Guid sourceId, CancellationToken token) => throw new NotSupportedException();
        public Task<EquipmentUsageDto> SaveUsageAsync(EquipmentActor actor, SaveEquipmentUsageRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<EquipmentUsageHistoryDto>> ListUsagesAsync(EquipmentActor actor, EquipmentUsageFilter filter, CancellationToken token)
        {
            UsageQueryCount += 1;
            LastUsageFilter = filter;
            return Task.FromResult<IReadOnlyList<EquipmentUsageHistoryDto>>([
                new(
                    Guid.Parse("88888888-8888-8888-8888-888888888888"),
                    EquipmentId,
                    "EQ-TEST",
                    "测试挖掘机",
                    FakeProjectService.ProjectId,
                    "PRJ-001",
                    "测试项目",
                    FakeCompanyService.CompanyId,
                    "测试设备公司",
                    new DateOnly(2026, 3, 1),
                    null,
                    RentMode.Daily,
                    500m,
                    Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    [])
            ]);
        }
        public Task TransferOwnershipAsync(EquipmentActor actor, TransferEquipmentOwnershipRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<Guid> SaveMaintenanceAsync(EquipmentActor actor, SaveEquipmentMaintenanceRequest request, CancellationToken token) => throw new NotSupportedException();
        private static EquipmentDetailsDto CreateEquipment() => new(
            EquipmentId,
            "EQ-TEST",
            "测试挖掘机",
            "X1",
            "挖掘机",
            EquipmentOwnershipType.Rented,
            EquipmentStatus.InUse,
            null,
            FakePartnerService.PartnerId,
            500m,
            Guid.NewGuid(),
            "设备备注",
            FakeCompanyService.CompanyId,
            "测试设备公司",
            null,
            "测试出租方",
            new DateOnly(2026, 1, 1),
            300000m,
            "QC-TEST",
            new DateOnly(2026, 1, 2),
            new DateOnly(2027, 1, 1),
            Guid.NewGuid(),
            "设备合格证.pdf");
    }

    private sealed class FakeCompanyService : ICompanyManagementService
    {
        public static readonly Guid CompanyId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        public static readonly Guid InactiveCompanyId = Guid.Parse("45454545-4545-4545-4545-454545454545");
        public Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CompanyListItemDto>>([new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "EQ-COMP", "测试设备公司", "设备公司", "一般纳税人", "法人", true), new(InactiveCompanyId, "EQ-INACTIVE", "停用设备公司", "停用设备公司", "一般纳税人", "法人", false)]);
        public Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
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
        public Task<IReadOnlyList<CompanyCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyCategoryDto> SaveCategoryAsync(CompanyActor actor, SaveCompanyCategoryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyAccountDto> SaveAccountAsync(CompanyActor actor, SaveCompanyAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyCertificateDto> SaveCertificateAsync(CompanyActor actor, SaveCompanyCertificateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakePartnerService : IBusinessPartnerService
    {
        public static readonly Guid PartnerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        public Task<IReadOnlyList<BusinessPartnerDto>> ListAsync(string? search, BusinessPartnerRoleType? role, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BusinessPartnerDto>>([new(PartnerId, "LESSOR", "测试出租方", "测试出租方", null, null, [new PartnerRoleDto(BusinessPartnerRoleType.MiscellaneousSupplier, null, null, null)], [], 0, true, Guid.NewGuid())]);
        public Task<BusinessPartnerDto> CreateAsync(CreateBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto> CopyAsync(CopyBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto> UpdateAsync(string userId, UpdateBusinessPartnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LinkToProjectAsync(LinkPartnerToProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessPartnerDto?> GetAsync(Guid partnerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProjectService : IProjectService
    {
        public static readonly Guid ProjectId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        private static readonly ProjectDto Project = new(ProjectId, "PRJ-001", "测试项目", null, ProjectStage.UnderConstruction);

        public Task<IReadOnlyList<ProjectListItemDto>> ListProjectsAsync(string? search, ProjectStage? stage, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectListItemDto>>([new ProjectListItemDto(Project, new ProjectSummaryDto(0, 0, 0, 0, default, 0, 0))]);
        public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractDto> AddContractAsync(CreateContractRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractLineItemDto> AddLineItemAsync(CreateContractLineItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ContractLineItemDto> UpdateLineItemAsync(UpdateContractLineItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProjectListPageDto> SearchProjectsAsync(ProjectListActor actor, ProjectListQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProjectListOptionsDto> GetListOptionsAsync(ProjectListActor actor, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProjectDetailsDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeBusinessYearService : IBusinessYearService
    {
        private static readonly BusinessYearDto Current = new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "2026 业务年",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        public Task<BusinessYearDto> CreateAsync(CreateBusinessYearRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<BusinessYearDto>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BusinessYearDto>>([Current]);
        public Task<BusinessYearDto?> GetByDateAsync(DateOnly businessDate, CancellationToken cancellationToken) => Task.FromResult<BusinessYearDto?>(Current);
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
