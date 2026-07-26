using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Certificates;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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
    public async Task CompanyOverviewPagesShowEmployeeCountAndCompactPortfolioLayout()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var listHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Companies"));
        var detailsHtml = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=overview"));

        listHtml.Should().Contain("data-company-employee-count=\"1\"")
            .And.Contain("class=\"company-workspace company-workspace--overview\"")
            .And.Contain("class=\"company-dashboard-stack\"")
            .And.Contain("data-row-density=\"compact\"");
        detailsHtml.Should().Contain("data-company-employee-count=\"1\"");
    }

    [Fact]
    public async Task AdministratorCanOpenCategoryBatchEditorWithStatusAndDeleteControls()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Companies"));

        html.Should().Contain("data-inline-edit=\"company-categories\"")
            .And.Contain("class=\"company-category-create-grid\"")
            .And.Contain("编辑修改")
            .And.Contain("保存分类")
            .And.Contain("保存修改")
            .And.Contain("CategoryRows[0].Code")
            .And.Contain("CategoryRows[0].Name")
            .And.Contain("CategoryRows[0].SortOrder")
            .And.Contain("CategoryRows[0].IsActive")
            .And.Contain("DeleteCategory")
            .And.Contain(">删除</button>")
            .And.NotContain(">删除行</button>");
    }

    [Fact]
    public void CompanyPortfolioGivesCategoryEditorOneQuarterOfDesktopWidth()
    {
        var css = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "EngineeringManager.Web",
            "wwwroot",
            "css",
            "pages.css"));

        css.Should().Contain(".company-portfolio-grid { grid-template-columns: minmax(22rem, .5fr) minmax(0, 1.5fr);");
        css.Should().Contain("@media (max-width: 1380px)")
            .And.Contain(".company-portfolio-grid { grid-template-columns: 1fr; }");
    }

    [Fact]
    public async Task AdministratorCanSaveCategoryBatchAndDeleteUnusedCategory()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var companyService = (FakeCompanyService)factory.Services.GetRequiredService<ICompanyManagementService>();
        var token = await GetAntiforgeryTokenAsync(client, "/Companies");

        using var saved = await client.PostAsync("/Companies?handler=Categories", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CategoryRows[0].Id"] = FakeCompanyService.CategoryId.ToString(),
            ["CategoryRows[0].ConcurrencyStamp"] = FakeCompanyService.CategoryStamp.ToString(),
            ["CategoryRows[0].Code"] = "GENERAL-UPDATED",
            ["CategoryRows[0].Name"] = "更新后的分类名称",
            ["CategoryRows[0].SortOrder"] = "12",
            ["CategoryRows[0].IsActive"] = "false",
            ["__RequestVerificationToken"] = token
        }));
        token = await GetAntiforgeryTokenAsync(client, "/Companies");
        using var deleted = await client.PostAsync(
            $"/Companies?handler=DeleteCategory&categoryId={FakeCompanyService.CategoryId}&concurrencyStamp={FakeCompanyService.CategoryStamp}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        saved.StatusCode.Should().Be(HttpStatusCode.Redirect);
        companyService.SavedCategories.Should().ContainSingle(request =>
            request.Id == FakeCompanyService.CategoryId && request.Code == "GENERAL-UPDATED" && !request.IsActive);
        deleted.StatusCode.Should().Be(HttpStatusCode.Redirect);
        companyService.DeletedCategoryIds.Should().ContainSingle().Which.Should().Be(FakeCompanyService.CategoryId);
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
        html.Should().Contain("class=\"content-grid company-detail-full-grid\"");
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
            .And.Contain("快捷编辑")
            .And.Contain("停用");
    }

    [Fact]
    public async Task AdministratorAccountsTabUsesOneBatchEditorAndDropdownsWithoutActionColumn()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts"));

        html.Should().Contain("data-inline-edit=\"company-accounts\"")
            .And.Contain("id=\"account-batch-form\"")
            .And.Contain("快捷编辑")
            .And.Contain("取消编辑")
            .And.Contain("保存修改")
            .And.Contain("AccountRows[0].DefaultPurpose")
            .And.Contain("AccountRows[0].IsActive")
            .And.NotContain(">操作</th>")
            .And.NotContain("data-account-status-action");
    }

    [Fact]
    public async Task AdministratorCanSaveAccountBatchWithCombinedDefaultPurposeAndStatus()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var companyService = (FakeCompanyService)factory.Services.GetRequiredService<ICompanyManagementService>();
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts");

        using var response = await client.PostAsync(
            $"/Companies/Details/{FakeCompanyService.CompanyId}?handler=Accounts",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["AccountRows[0].Id"] = FakeCompanyService.AccountId.ToString(),
                ["AccountRows[0].ConcurrencyStamp"] = FakeCompanyService.AccountStamp.ToString(),
                ["AccountRows[0].Name"] = "更新后的基本户",
                ["AccountRows[0].Number"] = "62220001",
                ["AccountRows[0].BankName"] = "测试银行",
                ["AccountRows[0].AccountType"] = "1",
                ["AccountRows[0].OpeningBalance"] = "100.50",
                ["AccountRows[0].DefaultPurpose"] = "7",
                ["AccountRows[0].IsActive"] = "false",
                ["AccountRows[0].Notes"] = "批量更新备注",
                ["__RequestVerificationToken"] = token
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("tab=accounts");
        companyService.SavedAccounts.Should().ContainSingle();
        var request = companyService.SavedAccounts.Single();
        request.Id.Should().Be(FakeCompanyService.AccountId);
        request.IsDefaultCollection.Should().BeTrue();
        request.IsDefaultPayment.Should().BeTrue();
        request.IsDefaultInvoice.Should().BeTrue();
        request.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AccountBatchRejectsInvalidDropdownValueWithoutSaving()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();
        var companyService = (FakeCompanyService)factory.Services.GetRequiredService<ICompanyManagementService>();
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts");

        using var response = await client.PostAsync(
            $"/Companies/Details/{FakeCompanyService.CompanyId}?handler=Accounts",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["AccountRows[0].Id"] = FakeCompanyService.AccountId.ToString(),
                ["AccountRows[0].ConcurrencyStamp"] = FakeCompanyService.AccountStamp.ToString(),
                ["AccountRows[0].Name"] = "基本户",
                ["AccountRows[0].AccountType"] = "1",
                ["AccountRows[0].OpeningBalance"] = "0",
                ["AccountRows[0].DefaultPurpose"] = "invalid",
                ["AccountRows[0].IsActive"] = "true",
                ["__RequestVerificationToken"] = token
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        companyService.SavedAccounts.Should().BeEmpty();
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()).Should().Contain("data-inline-edit-active=\"true\"");
    }

    [Fact]
    public async Task CompanyListLinksOpenExplicitOverviewTab()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Companies"));

        html.Should().Contain($"class=\"company-name-link\" href=\"/Companies/Details/{FakeCompanyService.CompanyId}?tab=overview\"");
    }

    [Fact]
    public async Task AdministratorCertificatesTabProvidesInlineEditing()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates"));

        html.Should().Contain("data-company-certificate-table");
        html.Should().Contain("data-inline-edit=\"company-certificates\"");
        html.Should().Contain("name=\"CertificateRows[0].Id\"");
        html.Should().Contain("name=\"CertificateRows[0].ConcurrencyStamp\"");
        html.Should().Contain("handler=Certificates");
    }

    [Fact]
    public async Task AdministratorCertificatesAndAccountsUseHeaderCreateDialogsAndBatchEditors()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var certificates = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates"));
        var accounts = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts"));

        certificates.Should().Contain("data-company-certificate-create-open")
            .And.Contain("data-company-certificate-create-dialog")
            .And.Contain("id=\"certificate-batch-form\"")
            .And.Contain("name=\"CertificateRows[0].Id\"")
            .And.Contain("name=\"CertificateAttachmentFile\"")
            .And.NotContain(">操作</th>")
            .And.NotContain("data-inline-edit=\"company-certificate-");
        accounts.Should().Contain("data-company-account-create-open")
            .And.Contain("data-company-account-create-dialog")
            .And.Contain("id=\"account-batch-form\"");
    }

    [Fact]
    public async Task AdministratorCertificatesTabProvidesPerCertificateAttachmentActionsAndPreview()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates"));

        html.Should().Contain(">附件</th>")
            .And.Contain("data-auto-upload-picker")
            .And.Contain("data-auto-upload-input")
            .And.Contain("data-attachment-preview-trigger")
            .And.Contain("营业执照.pdf")
            .And.Contain("data-attachment-preview-dialog")
            .And.Contain("/js/components/attachment-picker.js")
            .And.Contain("/js/components/attachment-preview.js");
    }

    [Fact]
    public async Task AdministratorCanCreateCertificateWithAttachmentFromDialog()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var certificateService = (FakeCompanyCertificateService)factory.Services.GetRequiredService<ICompanyCertificateService>();
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(token), "__RequestVerificationToken");
        content.Add(new StringContent("安全生产许可证"), "Certificate.Type");
        content.Add(new StringContent("SAFE-001"), "Certificate.Number");
        content.Add(new StringContent("2026-01-01"), "Certificate.IssuedOn");
        content.Add(new StringContent("2030-01-01"), "Certificate.ExpiresOn");
        content.Add(new StringContent("弹窗新增证照"), "Certificate.Notes");
        content.Add(new StringContent("新增公司证照"), "Certificate.Reason");
        content.Add(new ByteArrayContent([9, 8, 7]), "CertificateAttachmentFile", "安全生产许可证.pdf");

        using var response = await client.PostAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates&handler=Certificate", content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        certificateService.LastSavedRequest.Should().NotBeNull();
        certificateService.LastSavedRequest!.Id.Should().BeNull();
        certificateService.LastSavedRequest.NewAttachment.Should().NotBeNull();
        certificateService.LastSavedRequest.NewAttachment!.OriginalFileName.Should().Be("安全生产许可证.pdf");
        certificateService.LastSavedRequest.NewAttachment.Content.Should().Equal(9, 8, 7);
    }

    [Fact]
    public async Task AdministratorCanBatchUpdateCompanyCertificates()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var certificateService = (FakeCompanyCertificateService)factory.Services.GetRequiredService<ICompanyCertificateService>();
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates");

        using var response = await client.PostAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates&handler=Certificates", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CertificateRows[0].Id"] = FakeCompanyCertificateService.CertificateId.ToString(),
            ["CertificateRows[0].ConcurrencyStamp"] = FakeCompanyCertificateService.InitialConcurrencyStamp.ToString(),
            ["CertificateRows[0].Type"] = "批量更新营业执照",
            ["CertificateRows[0].Number"] = "CERT-BATCH",
            ["CertificateRows[0].IssuedOn"] = "2026-04-01",
            ["CertificateRows[0].ExpiresOn"] = "2031-04-01",
            ["CertificateRows[0].Notes"] = "批量更新备注",
            ["__RequestVerificationToken"] = token
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        certificateService.LastSavedRequests.Should().ContainSingle();
        certificateService.LastSavedRequests[0].CertificateType.Should().Be("批量更新营业执照");
        certificateService.LastSavedRequests[0].Reason.Should().Be("批量修改公司证照");
    }

    [Fact]
    public async Task InvalidAccountCreateKeepsCreateDialogOpen()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts");

        using var response = await client.PostAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts&handler=Account", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Account.Name"] = string.Empty,
            ["Account.AccountType"] = "1",
            ["Account.IsActive"] = "true",
            ["__RequestVerificationToken"] = token
        }));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("data-company-account-create-dialog")
            .And.Contain("data-dialog-open=\"true\"");
    }

    [Fact]
    public async Task AdministratorCanReplaceCertificateAttachmentWithoutChangingCertificateDetails()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var certificateService = (FakeCompanyCertificateService)factory.Services.GetRequiredService<ICompanyCertificateService>();
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(token), "__RequestVerificationToken");
        content.Add(new ByteArrayContent([1, 2, 3, 4]), "CertificateAttachmentFile", "更新营业执照.pdf");

        using var response = await client.PostAsync(
            $"/Companies/Details/{FakeCompanyService.CompanyId}?handler=CertificateAttachment&certificateId={FakeCompanyCertificateService.CertificateId}",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("tab=certificates");
        certificateService.LastSavedRequest.Should().NotBeNull();
        certificateService.LastSavedRequest!.CertificateType.Should().Be("营业执照");
        certificateService.LastSavedRequest.SpecialtyLevelScope.Should().Be("建筑二级");
        certificateService.LastSavedRequest.NewAttachment.Should().NotBeNull();
        certificateService.LastSavedRequest.NewAttachment!.OriginalFileName.Should().Be("更新营业执照.pdf");
        certificateService.LastSavedRequest.NewAttachment.Content.Should().Equal(1, 2, 3, 4);
        certificateService.LastSavedRequest.RemoveAttachment.Should().BeFalse();
    }

    [Fact]
    public async Task CertificateAttachmentCanBePreviewedDownloadedAndDeleted()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var certificateService = (FakeCompanyCertificateService)factory.Services.GetRequiredService<ICompanyCertificateService>();

        using var preview = await client.GetAsync(
            $"/Companies/Details/{FakeCompanyService.CompanyId}?handler=CertificateAttachment&certificateId={FakeCompanyCertificateService.CertificateId}");
        using var download = await client.GetAsync(
            $"/Companies/Details/{FakeCompanyService.CompanyId}?handler=CertificateAttachment&certificateId={FakeCompanyCertificateService.CertificateId}&download=true");
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates");
        using var deleted = await client.PostAsync(
            $"/Companies/Details/{FakeCompanyService.CompanyId}?handler=DeleteCertificateAttachment&certificateId={FakeCompanyCertificateService.CertificateId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        preview.Content.Headers.ContentDisposition.Should().BeNull();
        download.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("营业执照.pdf");
        deleted.StatusCode.Should().Be(HttpStatusCode.Redirect);
        certificateService.LastSavedRequest.Should().NotBeNull();
        certificateService.LastSavedRequest!.RemoveAttachment.Should().BeTrue();
        certificateService.LastSavedRequest.NewAttachment.Should().BeNull();
        certificateService.LastSavedRequest.CertificateType.Should().Be("营业执照");
    }

    [Fact]
    public async Task AdministratorCanUpdateCertificateInlineWithoutLosingExtendedFields()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var certificateService = (FakeCompanyCertificateService)factory.Services.GetRequiredService<ICompanyCertificateService>();
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates");

        using var response = await client.PostAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates&handler=Certificates", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CertificateRows[0].Id"] = FakeCompanyCertificateService.CertificateId.ToString(),
            ["CertificateRows[0].ConcurrencyStamp"] = FakeCompanyCertificateService.InitialConcurrencyStamp.ToString(),
            ["CertificateRows[0].Type"] = "更新营业执照",
            ["CertificateRows[0].Number"] = "CERT-002",
            ["CertificateRows[0].IssuedOn"] = "2026-02-01",
            ["CertificateRows[0].ExpiresOn"] = "2031-02-01",
            ["CertificateRows[0].Notes"] = "更新证书备注",
            ["__RequestVerificationToken"] = token
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("tab=certificates");
        certificateService.LastSavedRequests.Should().ContainSingle();
        certificateService.LastSavedRequests[0].SpecialtyLevelScope.Should().Be("建筑二级");
        certificateService.LastSavedRequests[0].IssuingAuthority.Should().Be("住建部门");
        certificateService.LastSavedRequests[0].NewAttachment.Should().BeNull();
        certificateService.LastSavedRequests[0].RemoveAttachment.Should().BeFalse();
        certificateService.LastSavedRequests[0].ConcurrencyStamp.Should().Be(FakeCompanyCertificateService.InitialConcurrencyStamp);
    }

    [Fact]
    public async Task CertificateConcurrencyConflictKeepsOldStampAndRequiresRefresh()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var certificateService = (FakeCompanyCertificateService)factory.Services.GetRequiredService<ICompanyCertificateService>();
        certificateService.ThrowConcurrencyOnSave = true;
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates");

        using var response = await client.PostAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates&handler=Certificates", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CertificateRows[0].Id"] = FakeCompanyCertificateService.CertificateId.ToString(),
            ["CertificateRows[0].ConcurrencyStamp"] = FakeCompanyCertificateService.InitialConcurrencyStamp.ToString(),
            ["CertificateRows[0].Type"] = "冲突前的本地修改",
            ["CertificateRows[0].Number"] = "CERT-LOCAL",
            ["CertificateRows[0].IssuedOn"] = "2026-03-01",
            ["CertificateRows[0].ExpiresOn"] = "2031-03-01",
            ["CertificateRows[0].Notes"] = "本地备注",
            ["__RequestVerificationToken"] = token
        }));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("数据已被他人更新，请刷新后重试。");
        html.Should().Contain("data-inline-edit-active=\"true\"");
        html.Should().Contain($"name=\"CertificateRows[0].ConcurrencyStamp\" value=\"{FakeCompanyCertificateService.InitialConcurrencyStamp}\"");
        html.Should().Contain("value=\"冲突前的本地修改\"");
        html.Should().NotContain($"name=\"CertificateRows[0].ConcurrencyStamp\" value=\"{FakeCompanyCertificateService.NewerConcurrencyStamp}\"");
    }

    [Fact]
    public async Task InvalidCertificateDateStaysOnTabWithoutSaving()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var certificateService = (FakeCompanyCertificateService)factory.Services.GetRequiredService<ICompanyCertificateService>();
        var token = await GetAntiforgeryTokenAsync(client, $"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates");

        using var response = await client.PostAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=certificates&handler=Certificate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Certificate.Type"] = "日期校验证书",
            ["Certificate.Number"] = "CERT-DATE",
            ["Certificate.IssuedOn"] = "not-a-date",
            ["Certificate.ExpiresOn"] = "2031-03-01",
            ["Certificate.Notes"] = "日期校验",
            ["Certificate.Reason"] = "新增公司证照",
            ["__RequestVerificationToken"] = token
        }));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        certificateService.LastSavedRequest.Should().BeNull();
        html.Should().Contain("validation-summary-errors");
        html.Should().Contain("tab=certificates");
        html.Should().Contain("data-dialog-open=\"true\"");
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
    public async Task AdministratorAccountsTabUsesStatusDropdownWithoutDeleteAction()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Companies/Details/{FakeCompanyService.CompanyId}?tab=accounts"));
        html.Should().Contain("AccountRows[0].IsActive");
        html.Should().Contain(">停用</option>");
        html.Should().NotContain("确认删除这个账户吗");
        html.Should().NotContain("data-account-status-action");
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
                services.RemoveAll<ICompanyCertificateService>();
                services.AddSingleton<ICompanyCertificateService, FakeCompanyCertificateService>();
                services.RemoveAll<ICompanyActorService>();
                services.AddSingleton<ICompanyActorService, FakeCompanyActorService>();
                services.RemoveAll<IEmployeeService>();
                services.AddSingleton<IEmployeeService, FakeEmployeeService>();
            });
        });

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        match.Success.Should().BeTrue("Razor form should render an antiforgery token");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class FakeCompanyActorService : ICompanyActorService
    {
        public Task<CompanyActor> ResolveAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanyActor(userId, roles.Contains("ApplicationAdministrator"), true, []));
    }

    private sealed class FakeEmployeeService : IEmployeeService
    {
        private static readonly EmployeeDto Employee = new(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "E-001",
            "测试员工",
            EmployeeType.Formal,
            null,
            "项目经理",
            FakeCompanyService.CompanyId,
            null,
            null,
            null,
            null,
            true,
            []);

        public Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EmployeeDto>>([Employee]);

        public Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto> CopyAsync(CopyEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto> UpdateAsync(string userId, UpdateEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeAffiliationDto> AddAffiliationAsync(CreateEmployeeAffiliationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCompanyService : ICompanyManagementService
    {
        public static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        public static readonly Guid CategoryStamp = Guid.Parse("88888888-8888-8888-8888-888888888888");
        public static readonly Guid AccountId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        public static readonly Guid AccountStamp = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public List<SaveCompanyCategoryRequest> SavedCategories { get; } = [];
        public List<Guid> DeletedCategoryIds { get; } = [];
        public List<SaveCompanyAccountRequest> SavedAccounts { get; } = [];

        public Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyListItemDto>>([new(CompanyId, "TEST", "测试自有公司", "测试公司", "一般纳税人有限公司", "测试法人", true, null, 1, 1)]);

        public Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanyDashboardDto(1, 1000m, 800m, 0m, 600m, 400m, 300m, 100m, 200m, 50m, 80m, 0m, 500m, DateTimeOffset.UtcNow));

        public Task<IReadOnlyList<CompanyCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CompanyCategoryDto>>([new(CategoryId, "GENERAL", "一般纳税人有限公司", 10, true, CategoryStamp)]);
        public Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken) => Task.FromResult(new CompanyDetailsDto(CompanyId, "TEST", "测试自有公司", "测试公司", CategoryId, "一般纳税人有限公司", "测试法人", "913000000000000001", "注册地址", "经营地址", "13800000000", "测试开票抬头", null, true, Guid.NewGuid(), [new(AccountId, "基本户", null, null, "Bank", 0m, false, false, false, true, "账户备注", AccountStamp)], []));
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
        public Task<CompanyCategoryDto> SaveCategoryAsync(CompanyActor actor, SaveCompanyCategoryRequest request, CancellationToken cancellationToken)
        {
            SavedCategories.Add(request);
            return Task.FromResult(new CompanyCategoryDto(request.Id ?? CategoryId, request.Code, request.Name, request.SortOrder, request.IsActive, Guid.NewGuid()));
        }
        public async Task<IReadOnlyList<CompanyCategoryDto>> SaveCategoriesAsync(CompanyActor actor, IReadOnlyList<SaveCompanyCategoryRequest> requests, CancellationToken cancellationToken)
        {
            var results = new List<CompanyCategoryDto>();
            foreach (var request in requests) results.Add(await SaveCategoryAsync(actor, request, cancellationToken));
            return results;
        }
        public Task DeleteCategoryAsync(CompanyActor actor, Guid id, Guid concurrencyStamp, CancellationToken cancellationToken)
        {
            DeletedCategoryIds.Add(id);
            return Task.CompletedTask;
        }
        public Task<CompanyAccountDto> SaveAccountAsync(CompanyActor actor, SaveCompanyAccountRequest request, CancellationToken cancellationToken)
        {
            SavedAccounts.Add(request);
            return Task.FromResult(new CompanyAccountDto(request.Id ?? AccountId, request.AccountName, request.AccountNumber, request.BankName,
                ((FinancialAccountType)request.AccountType).ToString(), request.OpeningBalance, request.IsDefaultCollection,
                request.IsDefaultPayment, request.IsDefaultInvoice, request.IsActive, request.Notes, Guid.NewGuid()));
        }
        public async Task<IReadOnlyList<CompanyAccountDto>> SaveAccountsAsync(CompanyActor actor, IReadOnlyList<SaveCompanyAccountRequest> requests, CancellationToken cancellationToken)
        {
            var results = new List<CompanyAccountDto>();
            foreach (var request in requests) results.Add(await SaveAccountAsync(actor, request, cancellationToken));
            return results;
        }
        public Task<CompanyCertificateDto> SaveCertificateAsync(CompanyActor actor, SaveCompanyCertificateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCompanyCertificateService : ICompanyCertificateService
    {
        public static readonly Guid CertificateId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        public static readonly Guid AttachmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        public static readonly Guid InitialConcurrencyStamp = Guid.Parse("44444444-4444-4444-4444-444444444444");
        public static readonly Guid NewerConcurrencyStamp = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private CompanyCertificateItemDto currentItem = CreateItem(InitialConcurrencyStamp);

        public SaveCompanyCertificateItemRequest? LastSavedRequest { get; private set; }
        public SaveCompanyCertificateItemRequest[] LastSavedRequests { get; private set; } = [];
        public bool ThrowConcurrencyOnSave { get; set; }

        private static CompanyCertificateItemDto CreateItem(Guid concurrencyStamp) => new(
            CertificateId,
            FakeCompanyService.CompanyId,
            "TEST",
            "测试自有公司",
            "营业执照",
            "CERT-001",
            "建筑二级",
            "住建部门",
            new DateOnly(2026, 1, 1),
            new DateOnly(2030, 12, 31),
            AttachmentId,
            "营业执照.pdf",
            "证书备注",
            CertificateExpiryState.Normal,
            concurrencyStamp);

        public Task<IReadOnlyList<CompanyCertificateItemDto>> ListAsync(CompanyActor actor, CertificateFilter filter, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyCertificateItemDto>>([currentItem]);

        public Task<CompanyCertificateItemDto> GetAsync(CompanyActor actor, Guid id, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult(currentItem);

        public Task<CompanyCertificateItemDto> SaveAsync(CompanyActor actor, SaveCompanyCertificateItemRequest request, DateOnly today, CancellationToken cancellationToken)
        {
            LastSavedRequest = request;
            if (ThrowConcurrencyOnSave)
            {
                currentItem = CreateItem(NewerConcurrencyStamp) with { CertificateType = "他人已修改证书" };
                throw new DbUpdateConcurrencyException("公司证书已被其他用户修改。");
            }

            currentItem = currentItem with
            {
                CertificateType = request.CertificateType,
                CertificateNumber = request.CertificateNumber,
                IssuedOn = request.IssuedOn,
                ExpiresOn = request.ExpiresOn,
                Notes = request.Notes,
                AttachmentId = request.RemoveAttachment ? null : request.NewAttachment is null ? currentItem.AttachmentId : AttachmentId,
                AttachmentFileName = request.RemoveAttachment ? null : request.NewAttachment?.OriginalFileName ?? currentItem.AttachmentFileName,
                ConcurrencyStamp = NewerConcurrencyStamp
            };
            return Task.FromResult(currentItem);
        }

        public async Task<IReadOnlyList<CompanyCertificateItemDto>> SaveManyAsync(CompanyActor actor, IReadOnlyList<SaveCompanyCertificateItemRequest> requests, DateOnly today, CancellationToken cancellationToken)
        {
            LastSavedRequests = requests.ToArray();
            var saved = new List<CompanyCertificateItemDto>();
            foreach (var request in requests) saved.Add(await SaveAsync(actor, request, today, cancellationToken));
            return saved;
        }

        public Task DeleteAsync(CompanyActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CertificateFileDto> DownloadAttachmentAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(new CertificateFileDto("营业执照.pdf", "application/pdf", [1, 2, 3, 4]));
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
