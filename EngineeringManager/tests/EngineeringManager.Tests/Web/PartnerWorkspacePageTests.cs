using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class PartnerWorkspacePageTests
{
    [Fact]
    public void PartnerIndexUsesWorkspaceFiltersCompactColumnsAndModalActions()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml.cs");
        var presets = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "DataWorkbenchPresets.cs");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "partner-workspace.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        var roleFilterPosition = page.IndexOf("new(\"Role\", \"角色\"", StringComparison.Ordinal);
        var searchFilterPosition = page.IndexOf("new(\"Search\", \"搜索\"", StringComparison.Ordinal);
        var statusFilterPosition = page.IndexOf("new(\"IsActive\", \"状态\"", StringComparison.Ordinal);

        roleFilterPosition.Should().BeLessThan(searchFilterPosition);
        searchFilterPosition.Should().BeLessThan(statusFilterPosition);

        page.Should().Contain("data-partner-workspace")
            .And.Contain("partner-workspace-layout")
            .And.Contain("equipment-list-toolbar equipment-list-toolbar--integrated partner-list-toolbar")
            .And.Contain("new(\"Role\", \"角色\"")
            .And.Contain("new(\"IsActive\", \"状态\"")
            .And.Contain("data-column-key=\"partner\"")
            .And.Contain("data-column-key=\"role_trade\"")
            .And.Contain("data-column-key=\"contact\"")
            .And.Contain("data-column-key=\"receipts\"")
            .And.Contain("data-column-key=\"invoices\"")
            .And.Contain("data-column-key=\"payments\"")
            .And.Contain("data-partner-financial-summary")
            .And.Contain("role=\"progressbar\"")
            .And.Contain("aria-label=\"应收完成进度\"")
            .And.Contain("aria-label=\"应付完成进度\"")
            .And.Contain("aria-label=\"开票完成进度\"")
            .And.Contain("var receivableProgressState = FinancialProgressState")
            .And.Contain("var payableProgressState = FinancialProgressState")
            .And.Contain("var invoiceProgressState = FinancialProgressState")
            .And.Contain("partner-financial-progress-shell")
            .And.Contain("partner-financial-progress-value")
            .And.Contain("@receivableProgressLabel")
            .And.Contain("@payableProgressLabel")
            .And.Contain("@invoiceProgressLabel")
            .And.Contain("var financialColumnCount = Model.CanViewFinance ? 3 : 0")
            .And.Contain("colspan=\"@(6 + financialColumnCount)\"")
            .And.Contain("<small>应收</small>")
            .And.Contain("<small>已收</small>")
            .And.Contain("<small>未收</small>")
            .And.Contain("<small>应付</small>")
            .And.Contain("<small>已付</small>")
            .And.Contain("<small>未付</small>")
            .And.Contain("<small>应开票</small>")
            .And.Contain("<small>已开票</small>")
            .And.Contain("<small>未开票</small>")
            .And.Contain("data-partner-dialog-open=\"details\"")
            .And.Contain("data-partner-dialog-open=\"edit\"")
            .And.Contain("data-partner-dialog-open=\"copy\"")
            .And.Contain("data-partner-dialog-open=\"finance\"")
            .And.Contain("data-partner-finance-link")
            .And.Contain("data-partner-finance-dialog")
            .And.Contain("data-partner-finance-jump")
            .And.Contain("data-partner-finance-chart=\"receivable\"")
            .And.Contain("data-partner-finance-chart=\"payable\"")
            .And.Contain("data-partner-finance-chart=\"salesInvoice\"")
            .And.Contain("data-partner-finance-chart=\"purchaseInvoice\"")
            .And.Contain("data-partner-finance-metric=\"receivable.grossSettlementAmount\"")
            .And.Contain("data-partner-finance-metric=\"payable.overInvoiced\"")
            .And.Contain("data-partner-editor-dialog")
            .And.Contain("data-partner-details-dialog")
            .And.Contain("data-partner-status-section hidden")
            .And.Contain("mac-window-dialog")
            .And.NotContain("data-inline-cell-edit")
            .And.NotContain("快捷编辑合作单位")
            .And.NotContain("data-partner-delete");

        model.Should().Contain("public IReadOnlyList<BusinessPartnerDto> AllPartners")
            .And.Contain("public bool CanViewFinance")
            .And.Contain("public IReadOnlyDictionary<Guid, PartnerLedgerSummaryDto> PartnerFinancialSummaries")
            .And.Contain("GetPartnerSummariesAsync(")
            .And.Contain("[BindProperty(SupportsGet = true)] public BusinessPartnerRoleType? Role")
            .And.Contain("[BindProperty(SupportsGet = true)] public bool? IsActive")
            .And.Contain("OnPostSaveAsync")
            .And.Contain("new CreateBusinessPartnerRequest(")
            .And.Contain("new UpdateBusinessPartnerRequest(")
            .And.Contain("ActiveDialog = \"editor\"");

        presets.Should().Contain("(\"receipts\", \"应收\")")
            .And.Contain("(\"invoices\", \"开票\")")
            .And.Contain("(\"payments\", \"应付\")");

        script.Should().Contain("form.querySelectorAll(\"select\")")
            .And.Contain("const financeDialog = page.querySelector(\"[data-partner-finance-dialog]\")")
            .And.Contain("const openFinance = (payload) =>")
            .And.Contain("data-partner-finance-metric")
            .And.Contain("data-partner-finance-chart")
            .And.Contain("mode === \"finance\"")
            .And.Contain("jump.href = payload.financeUrl")
            .And.Contain("addEventListener(\"change\", () => form.requestSubmit())")
            .And.Contain("copy ? `${payload.partnerNumber}-COPY` : payload.partnerNumber")
            .And.Contain("setField(\"ConcurrencyStamp\", editing ? payload.concurrencyStamp : \"00000000-0000-0000-0000-000000000000\")")
            .And.Contain("setField(\"UnifiedSocialCreditCode\", copy ? \"\" : payload.unifiedSocialCreditCode)")
            .And.Contain("setField(\"ContactName\", copy ? \"\" : payload.contactName)")
            .And.Contain("setField(\"IsActive\", editing ? payload.isActive : true)")
            .And.Contain("statusSection.hidden = !editing")
            .And.Contain("statusSection.hidden = !field(\"Id\")?.value");

        css.Should().Contain(".partner-workspace-layout { display: grid; grid-template-columns: minmax(210px, 220px) minmax(0, 1fr);")
            .And.Contain("align-items: stretch")
            .And.Contain(".partner-workspace-table { width: 100%;")
            .And.Contain(".partner-financial-cell")
            .And.Contain(".partner-financial-progress")
            .And.Contain(".partner-financial-progress-shell { position: relative;")
            .And.Contain(".partner-financial-progress-value { position: absolute; inset: 0; display: grid; place-items: center; color: #fff;")
            .And.Contain(".partner-finance-dialog { width: min(76rem, calc(100vw - 2rem));")
            .And.Contain(".partner-finance-chart")
            .And.Contain(".partner-finance-detail-grid")
            .And.Contain("th[data-column-key=\"role_trade\"] { width: 7rem; }")
            .And.Contain("th[data-column-key=\"contact\"] { width: 7rem; }")
            .And.Contain("th[data-column-key=\"projects\"] { width: 4.75rem; }")
            .And.Contain("font-variant-numeric: tabular-nums")
            .And.Contain(".partner-row-actions { display: flex; flex-wrap: nowrap;")
            .And.Contain(".partner-editor-dialog { width: min(68rem, calc(100vw - 2rem));")
            .And.Contain(".partner-editor-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr));")
            .And.Contain(".partner-workspace-layout { grid-template-columns: 1fr; }")
            .And.NotContain(".partner-list-toolbar .data-workbench-toolbar")
            .And.NotContain(".partner-list-toolbar .workbench-inline-filters");
    }

    [Fact]
    public void PartnerFinanceNumbersUseCategoryColorsStateHierarchyAndZeroValueDeemphasis()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "partner-workspace.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("partner-financial-cell--receivable")
            .And.Contain("partner-financial-cell--payable")
            .And.Contain("partner-financial-cell--invoice")
            .And.Contain("partner-financial-line--target")
            .And.Contain("partner-financial-line--completed")
            .And.Contain("partner-financial-line--pending")
            .And.Contain("data-partner-finance-legend")
            .And.Contain("data-partner-finance-tone=\"target\"")
            .And.Contain("data-partner-finance-tone=\"completed\"")
            .And.Contain("data-partner-finance-tone=\"pending\"")
            .And.Contain("data-partner-finance-tone=\"exception\"")
            .And.Contain("partner-finance-detail-section--receivable")
            .And.Contain("partner-finance-detail-section--payable");

        script.Should().Contain("target.classList.toggle(\"is-zero\", numericValue === 0)")
            .And.Contain("remaining?.classList.toggle(\"is-zero\", remainingAmount === 0)");

        css.Should().Contain(".partner-financial-cell--receivable { --partner-financial-accent:")
            .And.Contain(".partner-financial-line--completed strong { color: var(--partner-financial-accent);")
            .And.Contain(".partner-financial-line--pending:not(.is-zero) strong")
            .And.Contain("[data-partner-finance-tone=\"exception\"]:not(.is-zero)")
            .And.Contain("[data-partner-finance-tone].is-zero")
            .And.Contain(".partner-finance-legend");
    }

    [Fact]
    public void PartnerFinancialProgressUsesRatioStatesAndKeepsActualOveragePercentages()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "partner-workspace.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("FinancialProgressState(decimal target, decimal completed)")
            .And.Contain("FinancialProgressLabel(decimal target, decimal completed)")
            .And.Contain("FinancialProgressValue(decimal target, decimal completed)")
            .And.Contain("< 30m => \"critical\"")
            .And.Contain("< 60m => \"low\"")
            .And.Contain("< 85m => \"medium\"")
            .And.Contain("< 100m => \"near\"")
            .And.Contain("100m => \"complete\"")
            .And.Contain("partner-financial-cell--state-@receivableProgressState")
            .And.Contain("partner-financial-cell--state-@payableProgressState")
            .And.Contain("partner-financial-cell--state-@invoiceProgressState")
            .And.Contain("data-partner-finance-state-source=\"receivable\"")
            .And.Contain("data-partner-finance-state-source=\"payable\"")
            .And.Contain("data-partner-finance-state-source=\"salesInvoice\"")
            .And.Contain("data-partner-finance-state-source=\"purchaseInvoice\"");

        script.Should().Contain("const progressStateFor = (targetAmount, completedAmount) =>")
            .And.Contain("const rawPercentage = targetAmount > 0")
            .And.Contain("const normalizedPercentage = targetAmount > 0")
            .And.Contain(": completedAmount > 0 ? 100 : 0;")
            .And.Contain("chart.dataset.progressState = progressState")
            .And.Contain("target.dataset.partnerFinanceStateSource === source")
            .And.Contain("target.dataset.progressState = progressState");

        css.Should().Contain("[data-progress-state=\"no-target\"]")
            .And.Contain("[data-progress-state=\"critical\"]")
            .And.Contain("[data-progress-state=\"low\"]")
            .And.Contain("[data-progress-state=\"medium\"]")
            .And.Contain("[data-progress-state=\"near\"]")
            .And.Contain("[data-progress-state=\"complete\"]")
            .And.Contain("[data-progress-state=\"over\"]")
            .And.Contain("--partner-progress-state-color")
            .And.Contain("var(--partner-progress-state-color)")
            .And.Contain("[data-progress-state=\"low\"] { --partner-progress-state-color: #c2410c; }")
            .And.Contain("[data-progress-state=\"medium\"] { --partner-progress-state-color: #a16207; }")
            .And.Contain("box-shadow: inset 0 2px 0 var(--partner-financial-accent)")
            .And.Contain("[data-partner-finance-tone].is-zero:not([data-partner-finance-state-source])");
    }

    [Fact]
    public void PartnerDetailsAndFinanceDialogsShareCloseLeftFinanceRightActions()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var detailsStart = page.IndexOf("data-partner-details-dialog", StringComparison.Ordinal);
        var detailsEnd = page.IndexOf("</dialog>", detailsStart, StringComparison.Ordinal);
        var detailsDialog = page[detailsStart..detailsEnd];
        var financeStart = page.IndexOf("data-partner-finance-dialog", StringComparison.Ordinal);
        var financeEnd = page.IndexOf("</dialog>", financeStart, StringComparison.Ordinal);
        var financeDialog = page[financeStart..financeEnd];

        (page.Split("partner-dialog-navigation-actions", StringSplitOptions.None).Length - 1).Should().Be(2);
        detailsDialog.IndexOf(">关闭</button>", StringComparison.Ordinal).Should()
            .BeLessThan(detailsDialog.IndexOf("equipment-dialog-action-spacer", StringComparison.Ordinal));
        detailsDialog.IndexOf("equipment-dialog-action-spacer", StringComparison.Ordinal).Should()
            .BeLessThan(detailsDialog.IndexOf("data-partner-detail-finance", StringComparison.Ordinal));
        financeDialog.IndexOf(">关闭</button>", StringComparison.Ordinal).Should()
            .BeLessThan(financeDialog.IndexOf("equipment-dialog-action-spacer", StringComparison.Ordinal));
        financeDialog.IndexOf("equipment-dialog-action-spacer", StringComparison.Ordinal).Should()
            .BeLessThan(financeDialog.IndexOf("data-partner-finance-jump", StringComparison.Ordinal));
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string ReadFileIfExists(params string[] parts)
    {
        var path = Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray());
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
