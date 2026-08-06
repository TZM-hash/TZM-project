using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringManager.Tests.Web;

public sealed class ConstructionCrewPageTests
{
    [Fact]
    public async Task CrewListRouteRedirectsToUnifiedPartnerCategory()
    {
        var model = new EngineeringManager.Web.Pages.Crews.IndexModel(null!, null!, null!, null!)
        {
            Search = "钢筋",
            IsActive = true
        };

        var result = await model.OnGetAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.PageName.Should().Be("/Partners/Index");
        redirect.RouteValues!["Category"].Should().Be(EngineeringManager.Web.Pages.Partners.IndexModel.CrewCategory);
        redirect.RouteValues["Search"].Should().Be("钢筋");
        redirect.RouteValues["IsActive"].Should().Be(true);
    }

    [Fact]
    public void CrewPagesExposeRosterProjectAndPayrollHistory()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var details = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Details.cshtml");

        index.Should().Contain("施工班组管理");
        index.Should().Contain("当前人员");
        index.Should().Contain("应付");
        index.Should().Contain("销项票");
        details.Should().Contain("人员名册");
        details.Should().Contain("民工工资发放记录");
        details.Should().Contain("查看来源批次");
        details.Should().Contain("asp-page-handler=\"AddWorker\"");
        details.Should().Contain("asp-page-handler=\"TransferWorker\"");
    }

    [Fact]
    public void CrewIndexMirrorsPartnerFinancialTableAndDialogs()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml.cs");
        var presets = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "DataWorkbenchPresets.cs");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "crew-workspace.js");

        index.Should().Contain("data-column-key=\"role_trade\"")
            .And.Contain("data-column-key=\"contact\"")
            .And.Contain("data-column-key=\"payments\"")
            .And.Contain("data-column-key=\"invoices\"")
            .And.Contain("partner-workspace-table--financial")
            .And.Contain("partner-financial-cell--payable")
            .And.Contain("partner-financial-cell--invoice")
            .And.Contain("aria-label=\"应付完成进度\"")
            .And.Contain("aria-label=\"销项票完成进度\"")
            .And.Contain("var financialColumnCount = Model.CanViewFinance ? 2 : 0")
            .And.NotContain("data-column-key=\"receipts\"")
            .And.NotContain("partner-financial-cell--receivable")
            .And.NotContain("aria-label=\"应收完成进度\"")
            .And.Contain("data-crew-dialog-open=\"finance\"")
            .And.Contain("data-crew-finance-dialog")
            .And.Contain("data-crew-finance-jump")
            .And.Contain("data-crew-finance-chart=\"receivable\"")
            .And.Contain("data-crew-finance-chart=\"payable\"")
            .And.Contain("data-crew-finance-chart=\"salesInvoice\"")
            .And.Contain("data-crew-finance-chart=\"purchaseInvoice\"")
            .And.Contain("data-crew-finance-metric=\"receivable.grossSettlementAmount\"")
            .And.Contain("data-crew-finance-metric=\"payable.overInvoiced\"")
            .And.Contain("partner-editor-dialog")
            .And.Contain("partner-details-dialog")
            .And.Contain("partner-finance-dialog");

        model.Should().Contain("ICentralLedgerQueryService ledgerQueryService")
            .And.Contain("ApplicationDbContext db")
            .And.Contain("public bool CanViewFinance")
            .And.Contain("IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto> PartnerFinancialSummaries")
            .And.Contain("LedgerPageSupport.CreateActorAsync")
            .And.Contain("GetPartnerSummariesAsync(");

        presets.Should().Contain("(\"role_trade\", \"角色 / 专业\")")
            .And.Contain("(\"contact\", \"主要联系人\")")
            .And.Contain("(\"payments\", \"应付\")")
            .And.Contain("(\"invoices\", \"开票\")");

        script.Should().Contain("const financeDialog = page.querySelector(\"[data-crew-finance-dialog]\")")
            .And.Contain("const openFinance = (payload) =>")
            .And.Contain("data-crew-finance-metric")
            .And.Contain("data-crew-finance-chart")
            .And.Contain("mode === \"finance\"")
            .And.Contain("jump.href = payload.financeUrl");
    }

    [Fact]
    public void CrewOverviewUsesPayableAndSalesInvoiceColumnsWithoutReducingFinanceDialog()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var overview = page[..page.IndexOf("<dialog", StringComparison.Ordinal)];

        overview.Should().Contain("column.Key is \"payments\" or \"invoices\"")
            .And.Contain("column with { Label = \"销项票\" }")
            .And.Contain("<th data-column-key=\"payments\">应付</th>")
            .And.Contain("<th data-column-key=\"invoices\">销项票</th>")
            .And.Contain("financialSummary.Receivable.ShouldInvoiceAmount")
            .And.NotContain("data-column-key=\"receipts\"")
            .And.NotContain("partner-financial-cell--receivable");
        System.Text.RegularExpressions.Regex.Matches(overview, "data-column-key=\"invoices\"")
            .Should().HaveCount(2, "the overview needs one invoice header and one invoice cell per row template");

        page.Should().Contain("data-crew-finance-chart=\"receivable\"")
            .And.Contain("data-crew-finance-chart=\"payable\"")
            .And.Contain("data-crew-finance-chart=\"salesInvoice\"")
            .And.Contain("data-crew-finance-chart=\"purchaseInvoice\"");

        css.Should().Contain(".crew-workspace-table.partner-workspace-table--financial { min-width: 79rem; }")
            .And.Contain("th[data-column-key=\"crew\"] { width: 16rem; }")
            .And.Contain("th[data-column-key=\"role_trade\"] { width: 8.5rem; }")
            .And.Contain("th[data-column-key=\"contact\"] { width: 8.5rem; }")
            .And.Contain("th[data-column-key=\"projects\"] { width: 4.5rem; }")
            .And.Contain("th[data-column-key=\"payments\"] { width: 9rem; }");
    }

    [Fact]
    public void CrewIndexUsesPartnerStyleWorkspaceDialogsAndDeepLinks()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml.cs");
        var details = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Details.cshtml");
        var presets = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "DataWorkbenchPresets.cs");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "crew-workspace.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        index.Should().Contain("data-crew-workspace")
            .And.Contain("crew-workspace-layout")
            .And.Contain("crew-cell-ellipsis")
            .And.Contain("data-crew-dialog-open=\"create\"")
            .And.Contain("data-crew-dialog-open=\"details\"")
            .And.Contain("data-crew-dialog-open=\"edit\"")
            .And.Contain("data-crew-dialog-open=\"copy\"")
            .And.Contain("data-crew-dialog-open=\"roster\"")
            .And.Contain("#crew-finance")
            .And.Contain("data-crew-editor-dialog")
            .And.Contain("data-crew-details-dialog")
            .And.NotContain("data-crew-finance-legend")
            .And.NotContain("asp-page=\"/Partners/Create\"");

        model.Should().Contain("IBusinessPartnerService partnerService")
            .And.Contain("OnPostSaveAsync")
            .And.Contain("BusinessPartnerRoleType.ConstructionCrew")
            .And.Contain("public IReadOnlyList<CrewWorkspaceRow> AllCrews")
            .And.Contain("public string? Trade")
            .And.Contain("public bool? IsActive");

        details.Should().Contain("id=\"crew-roster\"")
            .And.Contain("id=\"crew-finance\"")
            .And.Contain("id=\"crew-payroll\"");

        presets.Should().Contain("(\"role_trade\", \"角色 / 专业\")")
            .And.Contain("(\"contact\", \"主要联系人\")")
            .And.Contain("(\"status\", \"状态\")");

        script.Should().Contain("[data-crew-workspace]")
            .And.Contain("mode === \"copy\"")
            .And.Contain("form.requestSubmit()")
            .And.Contain("data-crew-dialog-close");

        css.Should().Contain(".crew-workspace-layout")
            .And.Contain(".crew-workspace-table")
            .And.Contain(".crew-row-actions")
            .And.Contain(".partner-name-clamp, .crew-name-clamp")
            .And.Contain(".partner-cell-ellipsis, .crew-cell-ellipsis")
            .And.Contain(".partner-financial-progress-value { position: absolute; inset: 0; display: grid; place-items: center; color: #111827;")
            .And.Contain(".partner-finance-chart-heading > strong { color: #111827;")
            .And.Contain("@media (max-width: 1280px)")
            .And.Contain(".partner-workspace-layout, .crew-workspace-layout { grid-template-columns: 1fr; }");
    }

    [Fact]
    public void CrewRosterActionOpensAWorkspaceDialogWithLiveRosterData()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml.cs");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "crew-workspace.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        index.Should().Contain("data-crew-dialog-open=\"roster\"")
            .And.Contain("data-crew-roster-dialog")
            .And.Contain("data-crew-roster-table-body")
            .And.Contain("data-crew-roster-manage")
            .And.NotContain("<a class=\"action-button action-button--roster\"");

        model.Should().Contain("OnGetRosterAsync(Guid id")
            .And.Contain("details.Workers.Select(worker => new")
            .And.Contain("historicalWorkerCount = details.Workers.Count");

        script.Should().Contain("const rosterDialog = page.querySelector(\"[data-crew-roster-dialog]\")")
            .And.Contain("const openRoster = async (payload) =>")
            .And.Contain("fetch(`${window.location.pathname}?handler=Roster&id=${encodeURIComponent(payload.id)}`")
            .And.Contain("rosterBody.replaceChildren")
            .And.Contain("mode === \"roster\"");

        css.Should().Contain(".crew-roster-dialog")
            .And.Contain(".crew-roster-dialog-body")
            .And.Contain(".crew-roster-summary")
            .And.Contain(".crew-roster-table");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));
    private static string ReadFileIfExists(params string[] parts)
    {
        var path = Path.Combine(RepositoryRoot(), Path.Combine(parts));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
