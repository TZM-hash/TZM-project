using System.Security.Claims;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Web.Pages.Projects;

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

    [Fact]
    public async Task LegacyContractEditRouteReturnsNotFound()
    {
        await using var factory = CreateFactory("ProjectManager");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Projects/Contracts/Edit");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LegacyProjectRecordEditRouteReturnsNotFound()
    {
        await using var factory = CreateFactory("ProjectManager");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync($"/Projects/Records/Edit?projectId={FakeProjectWorkspaceService.ProjectId}&section=collection");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QuantityCreationRedirectsToCreatedLineWhenAttachmentUploadFails()
    {
        var workspaceService = new FakeProjectWorkspaceService();
        var model = new DetailsModel(
            workspaceService,
            new FakeProjectService(),
            new FakeFinanceLedgerService(),
            new FakeProjectConstructionService(),
            new FakeProjectWorkbookService(),
            new FakeProjectRecordAttachmentService())
        {
            PageContext = new PageContext(new ActionContext(
                new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "manager"), new Claim(ClaimTypes.Role, "ProjectManager")],
                        "Test"))
                },
                new RouteData(),
                new PageActionDescriptor())),
            CreateQuantity = new DetailsModel.CreateQuantityInput
            {
                ContractId = FakeProjectWorkspaceService.ContractId,
                Code = "Q-NEW",
                Name = "新增工程量",
                Unit = "项",
                Quantity = 1m,
                UnitPrice = 2m,
                AccountingLabel = "暂估",
                RequiresInvoice = true
            },
            QuantityAttachmentFile = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "附件.pdf")
            {
                Headers = new HeaderDictionary { ["Content-Type"] = "application/pdf" }
            }
        };
        model.TempData = new TempDataDictionary(model.HttpContext, new TestTempDataProvider());
        model.Url = new TestUrlHelper(model.PageContext);

        var result = await model.OnPostCreateQuantityAsync(FakeProjectWorkspaceService.ProjectId, CancellationToken.None);

        var errors = string.Join(" | ", model.ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));
        var redirect = result.Should().BeOfType<RedirectResult>($"ModelState errors: {errors}").Subject;
        redirect.Url.Should().EndWith($"#quantity-line-{FakeProjectWorkspaceService.LineItemId}");
        model.TempData["Error"].Should().Be("工程量已创建，但附件上传失败：模拟附件上传失败。");
    }

    [Fact]
    public async Task ProjectManagerSeesWorkspaceTabsAndInlineRecordActions()
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
        html.Should().NotContain("详细编辑");
        html.Should().NotContain("关联应收");
        html.Should().NotContain("发票方向");
        html.Should().Contain("新增工程量明细");
        html.Should().Contain("上传附件");
        html.Should().Contain("预览附件")
            .And.Contain("title=\"测试附件.pdf\"");
        html.Should().Contain("DeleteQuantityAttachment");
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
        html.Should().NotContain("项目里程碑");
        html.Should().NotContain("节点备注");
        html.Should().Contain("<h2>总包单位</h2>");
        html.Should().Contain("<h2>施工班组</h2>");
        html.Should().Contain("<h2>合作单位</h2>");
        html.Should().NotContain("<h2>项目人员</h2>");
        html.Should().Contain("合作单位").And.Contain("施工班组");
        html.Should().NotContain("合作备注");
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
    public void ProjectOverviewUsesUnifiedQuantityCompactLayoutAndScopedEditors()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "css", "pages.css"));

        page.Should().Contain("总包联系人 / 电话")
            .And.Contain("data-project-amount-view")
            .And.Contain("project-summary-half")
            .And.Contain("<th>工程量</th><th>单价</th><th>小计</th><th>口径</th><th>是否开票</th><th class=\"quantity-upload-column\">上传</th><th class=\"quantity-attachment-column\">附件</th>")
            .And.NotContain("<th>暂估工程量</th>")
            .And.NotContain("<th>结算工程量</th>")
            .And.NotContain("asp-page=\"/Projects/Records/Edit\"")
            .And.NotContain("关联应收")
            .And.NotContain("<th>方向</th>")
            .And.Contain("ProjectRecordAttachmentType.Settlement")
            .And.Contain("ProjectRecordAttachmentType.Invoice")
            .And.Contain("ProjectRecordAttachmentType.Cash")
            .And.Contain("ProjectRecordAttachmentType.Construction");
        styles.Should().Contain(".project-detail-strip article { height: 2.9rem; min-height: 0;")
            .And.Contain(".project-summary-grid > .project-summary-half { grid-column: span 2;");
    }

    [Fact]
    public void ProjectOverviewUsesCompactQuickEditSelectorsAndOnlyPersonnelAndParties()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml"));
        var selectorScript = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "js", "components", "check-selector.js"));
        var quickEditScript = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "js", "components", "quick-edit.js"));

        page.Should().Contain("project-legal-entity-selector")
            .And.Contain("project-tax-selector")
            .And.Contain("data-check-selector-clear")
            .And.Contain("data-project-amount-view-label")
            .And.Contain("project-amount-edit-control")
            .And.Contain("data-project-amount-view")
            .And.Contain("ProjectRelatedPartyBuilder.Build")
            .And.NotContain("<h2>项目里程碑</h2>")
            .And.Contain("<h2>总包单位</h2>")
            .And.Contain("<h2>施工班组</h2>").And.Contain("<h2>合作单位</h2>")
            .And.Contain("relatedParty.Roles");
        selectorScript.Should().Contain("data-check-selector-clear");
        quickEditScript.Should().Contain("querySelectorAll(\"[data-check-selector][open]\")")
            .And.Contain("querySelector(\"[data-check-selector-option]\")")
            .And.Contain("new KeyboardEvent(\"keydown\", { key: \"Escape\", bubbles: true })")
            .And.Contain("data-project-amount-view");
    }

    [Fact]
    public void DetailedRecordEditorFilesAreRemoved()
    {
        var root = RepositoryRoot();
        var recordsDirectory = Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Records");

        File.Exists(Path.Combine(recordsDirectory, "Edit.cshtml")).Should().BeFalse();
        File.Exists(Path.Combine(recordsDirectory, "Edit.cshtml.cs")).Should().BeFalse();
    }

    [Fact]
    public void FinanceYearAdministrationLivesUnderSystemSettings()
    {
        var root = RepositoryRoot();
        var admin = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Admin", "Index.cshtml"));
        var financeYear = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Admin", "FinanceYears", "Index.cshtml"));
        var legacyModel = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Ledger", "Years", "Index.cshtml.cs"));

        admin.Should().Contain("/Admin/FinanceYears/Index");
        financeYear.Should().Contain("所有自有公司、外部账本和内部账本统一使用同一套财务年度")
            .And.Contain("保存财务年度");
        legacyModel.Should().Contain("RedirectToPage(\"/Admin/FinanceYears/Index\")");
    }

    [Fact]
    public void ProjectPagesExposeContractTaxEquipmentAndFullWidthNotesWithoutArchiveField()
    {
        var root = RepositoryRoot();
        var details = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml"));
        var edit = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Edit.cshtml"));
        var financeCreate = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Finance", "Entries", "Create.cshtml"));

        details.Should().NotContain("归档状态")
            .And.NotContain("QuickEdit.ArchiveStatus")
            .And.Contain("合同签订")
            .And.Contain("data-project-tax-matrix")
            .And.Contain("施工机械")
            .And.Contain("显示在项目总览")
            .And.Contain("project-summary-notes");
        details.IndexOf("项目备注", StringComparison.Ordinal).Should().BeGreaterThan(details.IndexOf("应付 / 已付 / 未付", StringComparison.Ordinal));
        edit.Should().NotContain("归档状态")
            .And.Contain("合同签订状态")
            .And.Contain("data-project-tax-matrix");
        financeCreate.Should().Contain("开票公司")
            .And.Contain("ProjectTaxConfigurationId")
            .And.Contain("税率与发票类型");
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
        html.Should().NotContain("CreateQuantity");
        html.Should().NotContain(">上传附件</button>");
        html.Should().NotContain("DeleteQuantityAttachment");
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

    [Fact]
    public async Task ProjectManagerSeesWorkbookExportButSiteStaffDoesNot()
    {
        await using var managerFactory = CreateFactory("ProjectManager");
        using var managerClient = managerFactory.CreateClient();
        var managerHtml = System.Net.WebUtility.HtmlDecode(await (await managerClient.GetAsync("/Projects")).Content.ReadAsStringAsync());

        await using var staffFactory = CreateFactory("SiteStaff");
        using var staffClient = staffFactory.CreateClient();
        var staffHtml = System.Net.WebUtility.HtmlDecode(await (await staffClient.GetAsync("/Projects")).Content.ReadAsStringAsync());

        managerHtml.Should().Contain("ExportWorkbook")
            .And.Contain("data-project-workbook-export-menu")
            .And.Contain("导出项目清单")
            .And.Contain("选择项目工作簿细项")
            .And.NotContain("导出当前视图")
            .And.NotContain("project-workbook-export-form panel compact-form-grid");
        staffHtml.Should().NotContain("ExportWorkbook")
            .And.NotContain("data-project-workbook-export-menu")
            .And.NotContain("导出项目清单")
            .And.NotContain("选择项目工作簿细项");
    }

    [Fact]
    public void ProjectWorkbookSelectionDefaultsToManualAndHasMutualExclusionHooks()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml"));
        var exportPartial = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml"));
        var model = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs"));
        var script = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "js", "components", "check-selector.js"));
        var labels = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "DataExchange", "DataExchangeLabels.cs"));

        model.Should().Contain("SelectAllMatching { get; set; }").And.NotContain("SelectAllMatching { get; set; } = true");
        page.Should().Contain("data-project-export-item");
        exportPartial.Should().Contain("data-project-export-all-matching")
            .And.Contain("data-project-workbook-export-menu")
            .And.Contain("导出项目清单");
        script.Should().Contain("data-project-export-all-matching")
            .And.Contain("data-project-export-item")
            .And.Contain("form.elements");
        labels.Should().Contain("ProjectWorkbookSheet.Deductions => \"扣款\"");
    }

    [Fact]
    public void ProjectListExposesStableSerialColumnAndCompleteOverviewColumns()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml"));
        var model = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs"));

        page.Should().Contain("data-column-key=\"serial_number\"")
            .And.Contain("序号")
            .And.Contain("<td data-column-key=\"project_name\"><a asp-page=\"/Projects/Details\" asp-route-id=\"@item.Project.Id\">@item.Project.Name</a></td>")
            .And.Contain("data-column-key=\"general_contractor\"")
            .And.Contain("data-column-key=\"contract_signing_status\"")
            .And.Contain("data-column-key=\"actual_start_date\"")
            .And.Contain("data-column-key=\"actual_completion_date\"")
            .And.Contain("data-column-key=\"estimated_amount\"")
            .And.Contain("data-column-key=\"settled_amount\"")
            .And.Contain("data-column-key=\"contract_count\"")
            .And.Contain("data-column-key=\"line_item_count\"");
        model.Should().Contain("new(\"serial_number\", \"序号\", true, true)")
            .And.Contain("new(\"project_number\", \"项目编号\", true, false)")
            .And.Contain("new(\"general_contractor\", \"总包单位\")")
            .And.Contain("new(\"contract_signing_status\", \"合同签订\")");
    }

    [Fact]
    public void ProjectDetailInlineEditorsUseOneFormPerEditorAndKeepRouteId()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "wwwroot", "css", "components.css"));

        page.Should().Contain("id=\"project-overview-inline-form\"")
            .And.Contain("asp-route-id=\"@item.Overview.Id\"")
            .And.NotContain("<form id=\"finance-row")
            .And.NotContain("<form id=\"construction-row")
            .And.Contain("data-inline-edit=\"project-overview\"")
            .And.Contain("data-inline-edit=\"project-quantity\"")
            .And.Contain("data-inline-edit=\"project-collection\"")
            .And.Contain("data-inline-edit=\"project-invoice\"")
            .And.Contain("data-inline-edit=\"project-payment\"")
            .And.Contain("data-inline-edit=\"project-construction\"");
        const string overviewSelector = ".inline-edit-shell.project-overview-panel [data-inline-edit-control].inline-cell-control:not([hidden])";
        const string generalSelector = ".inline-edit-shell [data-inline-edit-control].inline-cell-control:not([hidden])";
        styles.Should().Contain(overviewSelector)
            .And.Contain(".project-tab-panel td { position: relative; }")
            .And.Contain(generalSelector + " { position: absolute;")
            .And.Contain(overviewSelector + " { position: static;");
        styles.IndexOf(overviewSelector, StringComparison.Ordinal)
            .Should().BeGreaterThan(styles.IndexOf(generalSelector, StringComparison.Ordinal));
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
                services.RemoveAll<IProjectWorkbookService>();
                services.AddSingleton<IProjectWorkbookService, FakeProjectWorkbookService>();
                services.RemoveAll<IProjectRecordAttachmentService>();
                services.AddSingleton<IProjectRecordAttachmentService, FakeProjectRecordAttachmentService>();
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
        public Task<ContractLineItemDto> AddLineItemAsync(CreateContractLineItemRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ContractLineItemDto(
                FakeProjectWorkspaceService.LineItemId,
                request.Code,
                request.Name,
                request.Unit,
                request.EstimatedQuantity,
                request.EstimatedUnitPrice,
                0m,
                request.SettledQuantity,
                request.SettledUnitPrice,
                0m,
                request.IsSettlementConfirmed,
                Guid.NewGuid(),
                request.Notes,
                request.Quantity,
                request.UnitPrice,
                request.AccountingLabel,
                request.RequiresInvoice));
        public Task<ContractLineItemDto> UpdateLineItemAsync(UpdateContractLineItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProjectListItemDto>> ListProjectsAsync(string? search, ProjectStage? stage, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectListItemDto>>([]);
        public Task<ProjectListPageDto> SearchProjectsAsync(ProjectListActor actor, ProjectListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectListPageDto(
                [new ProjectListItemDto(
                    new ProjectDto(FakeProjectWorkspaceService.ProjectId, "P-WEB-001", "项目工作台页面测试", "测试总包单位", ProjectStage.UnderConstruction, ProjectAffiliationType.ExternalPartyAttachedToUs),
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

    private sealed class FakeProjectWorkbookService : IProjectWorkbookService
    {
        public IReadOnlyList<ProjectWorkbookSheetDefinition> GetSheets() => [];
        public Task<ExportFileResult> ExportAsync(ProjectWorkbookExportRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ExportFileResult("projects.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", []));
        public Task<ProjectWorkbookImportPreviewDto> PreviewAsync(ProjectWorkbookImportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ConfirmAsync(ProjectWorkbookActor actor, Guid batchId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProjectRecordAttachmentService : IProjectRecordAttachmentService
    {
        public Task<IReadOnlyList<ProjectRecordAttachmentDto>> ListAsync(Guid projectId, ProjectRecordAttachmentType recordType, Guid recordId, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ProjectRecordAttachmentDto>>([
                new(Guid.Parse("71000000-0000-0000-0000-000000000099"), projectId, recordType, recordId, "测试附件.pdf", "application/pdf", 3, "页面测试附件", DateTimeOffset.UtcNow)
            ]);

        public Task<ProjectRecordAttachmentDto> UploadAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectRecordAttachmentDto> ReplaceAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, CancellationToken token) =>
            throw new IOException("模拟附件上传失败。");
        public Task<ProjectRecordAttachmentDto> ReplaceQuantityAsync(ProjectRecordAttachmentActor actor, ProjectRecordAttachmentUpload upload, CancellationToken token) =>
            throw new IOException("模拟附件上传失败。");
        public Task<ProjectRecordAttachmentFile> DownloadAsync(Guid projectId, Guid attachmentId, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(ProjectRecordAttachmentActor actor, Guid projectId, Guid attachmentId, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class FakeProjectWorkspaceService : IProjectWorkspaceService
    {
        public static readonly Guid ProjectId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        public static readonly Guid ContractId = Guid.Parse("71000000-0000-0000-0000-000000000010");
        public static readonly Guid LineItemId = Guid.Parse("71000000-0000-0000-0000-000000000011");

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
                    [new ProjectWorkspaceOptionDto(Guid.NewGuid().ToString(), "测试签约公司")],
                    new DateTimeOffset(2026, 7, 17, 8, 0, 0, TimeSpan.Zero),
                    Guid.Parse("71000000-0000-0000-0000-000000000002")),
                new ProjectSummaryDto(300m, 200m, 0m, 200m, ProjectSettlementStatus.Estimated, 1, 1),
                new FinanceProjectSummaryDto(ProjectId, 100m, 40m, 60m, 80m, 25m, 0m, 55m, 30m, 70m, 0m, false, false),
                [new ContractDto(ContractId, "C-WEB-001", "测试合同", ContractType.MainContract, ContractAllocationMode.SingleCompany, 300m,
                    [new ContractLineItemDto(LineItemId, "001", "土方工程", "m³", 10m, 20m, 200m, null, null, 0m, false, Guid.NewGuid())])],
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
        public Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
            ListProjectSummariesAsync(cancellationToken);
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
        public Task<ProjectConstructionRecordDto> LinkNextAsync(ProjectConstructionActor actor, LinkProjectConstructionRecordRequest request, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectConstructionRecordDto> LinkPreviousAsync(ProjectConstructionActor actor, LinkProjectConstructionRecordRequest request, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task<ProjectConstructionRecordDto> UnlinkAsync(ProjectConstructionActor actor, UnlinkProjectConstructionRecordRequest request, DateOnly today, CancellationToken token) => throw new NotSupportedException();
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

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;
        public string? Action(UrlActionContext actionContext) => null;
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => null;
        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }
}
