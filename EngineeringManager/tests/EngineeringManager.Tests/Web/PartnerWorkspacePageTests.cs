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
            .And.Contain("partner-cell-ellipsis")
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
            .And.Contain("invoiceColumnLabel + \"完成进度\"")
            .And.Contain("var receivableProgressState = FinancialProgressState")
            .And.Contain("var payableProgressState = FinancialProgressState")
            .And.Contain("var invoiceProgressState = FinancialProgressState")
            .And.Contain("partner-financial-progress-shell")
            .And.Contain("partner-financial-progress-value")
            .And.Contain("@receivableProgressLabel")
            .And.Contain("@payableProgressLabel")
            .And.Contain("@invoiceProgressLabel")
            .And.Contain("var financialColumnCount = Model.CanViewFinance ? 2 : 0")
            .And.Contain("colspan=\"@(6 + financialColumnCount)\"")
            .And.Contain("<small>应收</small>")
            .And.Contain("<small>已收</small>")
            .And.Contain("<small>未收</small>")
            .And.Contain("<small>应付</small>")
            .And.Contain("<small>已付</small>")
            .And.Contain("<small>未付</small>")
            .And.Contain("<small>@invoiceTargetLabel</small>")
            .And.Contain("<small>@invoiceCompletedLabel</small>")
            .And.Contain("<small>@invoicePendingLabel</small>")
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
            .And.Contain("const nextPartnerNumber = page.dataset.nextPartnerNumber")
            .And.Contain("setField(\"PartnerNumber\", editing ? payload.partnerNumber : nextPartnerNumber)")
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
            .And.Contain(".partner-financial-progress-value { position: absolute; inset: 0; display: grid; place-items: center; color: #111827;")
            .And.Contain(".partner-finance-dialog { width: min(76rem, calc(100vw - 2rem));")
            .And.Contain(".partner-finance-chart")
            .And.Contain(".partner-finance-detail-grid")
            .And.Contain(".partner-workspace-table--financial { min-width: 78rem; }")
            .And.Contain("th[data-column-key=\"partner\"] { width: 15.5rem; }")
            .And.Contain("th[data-column-key=\"role_trade\"] { width: 8.5rem; }")
            .And.Contain("th[data-column-key=\"contact\"] { width: 8.5rem; }")
            .And.Contain("th[data-column-key=\"projects\"] { width: 4.5rem; }")
            .And.Contain("th[data-column-key=\"payments\"] { width: 9rem; }")
            .And.Contain("font-variant-numeric: tabular-nums")
            .And.Contain(".partner-row-actions { display: flex; flex-wrap: nowrap;")
            .And.Contain(".partner-category-tabs { display: flex;")
            .And.Contain("overflow-x: auto")
            .And.Contain(".partner-name-clamp, .crew-name-clamp")
            .And.Contain("white-space: normal")
            .And.Contain(".partner-cell-ellipsis, .crew-cell-ellipsis")
            .And.Contain("text-overflow: ellipsis")
            .And.Contain(".partner-editor-dialog { width: min(68rem, calc(100vw - 2rem));")
            .And.Contain(".partner-editor-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr));")
            .And.Contain("@media (max-width: 1280px)")
            .And.Contain(".partner-workspace-layout, .crew-workspace-layout { grid-template-columns: 1fr; }")
            .And.NotContain(".partner-list-toolbar .data-workbench-toolbar")
            .And.NotContain(".partner-list-toolbar .workbench-inline-filters");
    }

    [Fact]
    public void PartnerOverviewUsesScopeSpecificCashAndInvoiceColumnsWithoutReducingFinanceDialogs()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");

        page.Should().Contain("var visibleFinancialColumnKeys = Model.IsCustomerScope")
            .And.Contain("new HashSet<string>(StringComparer.Ordinal) { \"receipts\", \"invoices\" }")
            .And.Contain("new HashSet<string>(StringComparer.Ordinal) { \"payments\", \"invoices\" }")
            .And.Contain("var invoiceColumnLabel = Model.IsCustomerScope ? \"进项票\" : \"销项票\"")
            .And.Contain("column with { Label = invoiceColumnLabel }")
            .And.Contain("@if (Model.IsCustomerScope)")
            .And.Contain("<th data-column-key=\"receipts\">应收</th>")
            .And.Contain("<th data-column-key=\"payments\">应付</th>")
            .And.Contain("<th data-column-key=\"invoices\">@invoiceColumnLabel</th>")
            .And.Contain("var invoiceSummary = Model.IsCustomerScope ? financialSummary.Payable : financialSummary.Receivable")
            .And.Contain("var invoiceTargetLabel = Model.IsCustomerScope ? \"应收票\" : \"应开票\"")
            .And.Contain("invoiceSummary.ShouldInvoiceAmount")
            .And.Contain("invoiceSummary.InvoicedAmount")
            .And.Contain("invoiceSummary.Uninvoiced")
            .And.Contain("新增@(Model.EntityLabel)")
            .And.NotContain("新增@Model.EntityLabel")
            .And.Contain("data-partner-finance-chart=\"receivable\"")
            .And.Contain("data-partner-finance-chart=\"payable\"")
            .And.Contain("data-partner-finance-chart=\"salesInvoice\"")
            .And.Contain("data-partner-finance-chart=\"purchaseInvoice\"");
    }

    [Fact]
    public void PartnerFinanceNumbersUseBusinessCategoryColorsAndZeroValueDeemphasis()
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
            .And.NotContain("data-partner-finance-legend")
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
            .And.NotContain(".partner-finance-legend");
    }

    [Fact]
    public void PartnerFinancialProgressUsesBusinessColorsAndKeepsActualOveragePercentages()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "partner-workspace.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("FinancialProgressState(decimal target, decimal completed)")
            .And.Contain("FinancialProgressLabel(decimal target, decimal completed)")
            .And.Contain("FinancialProgressValue(decimal target, decimal completed)")
            .And.Contain("if (percentage < 30m) return \"critical\";")
            .And.Contain("if (percentage < 60m) return \"low\";")
            .And.Contain("if (percentage < 85m) return \"medium\";")
            .And.Contain("if (percentage < 100m) return \"near\";")
            .And.Contain("return percentage == 100m ? \"complete\" : \"over\";")
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

        css.Should().Contain(".partner-financial-cell--receivable { --partner-financial-accent: #15803d; --partner-progress-fill: #86efac; }")
            .And.Contain(".partner-financial-cell--payable { --partner-financial-accent: #2563eb; --partner-progress-fill: #93c5fd; }")
            .And.Contain(".partner-financial-cell--invoice { --partner-financial-accent: #b45309; --partner-progress-fill: #fde68a; }")
            .And.Contain("[data-progress-state=\"no-target\"] { --partner-progress-fill: #cbd5e1; }")
            .And.Contain("[data-progress-state=\"over\"] { --partner-progress-fill: #fecaca; }")
            .And.Contain("background: var(--partner-progress-fill)")
            .And.Contain(".partner-financial-progress-value { position: absolute; inset: 0; display: grid; place-items: center; color: #111827;")
            .And.Contain(".partner-finance-chart-heading > strong { color: #111827;")
            .And.NotContain("--partner-progress-state-color")
            .And.NotContain("box-shadow: inset 0 2px 0 var(--partner-financial-accent)")
            .And.Contain("[data-partner-finance-tone].is-zero:not([data-partner-finance-state-source])");
    }

    [Fact]
    public void FinanceDialogsKeepEachLabelCloseToItsValueAndSeparateAdjacentGroups()
    {
        var partnerPage = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var crewPage = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        partnerPage.Should().Contain("partner-finance-chart-values")
            .And.Contain("partner-finance-detail-grid");
        crewPage.Should().Contain("partner-finance-chart-values")
            .And.Contain("partner-finance-detail-grid");

        css.Should().Contain(".partner-finance-chart-heading { display: flex; align-items: flex-start; justify-content: flex-start; gap: 1rem; }")
            .And.Contain(".partner-finance-chart-values { display: grid; grid-template-columns: repeat(3, minmax(0, auto)); gap: .45rem 1.25rem;")
            .And.Contain(".partner-finance-chart-values span { display: inline-flex; min-width: 0; justify-content: flex-start; gap: .35rem; }")
            .And.Contain(".partner-finance-detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 1.25rem;")
            .And.Contain(".partner-finance-detail-grid div { display: grid; grid-template-columns: 6.2rem max-content; min-width: 0; align-items: baseline; justify-content: start; gap: .5rem;");
    }

    [Fact]
    public void FinancialTableRowsKeepLabelsBesideNumbersOnPartnerAndCrewPages()
    {
        var partnerPage = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var crewPage = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        partnerPage.Should().Contain("partner-financial-lines");
        crewPage.Should().Contain("partner-financial-lines");

        css.Should().Contain(".partner-financial-lines > span { display: grid !important; grid-template-columns: max-content max-content; align-items: baseline; justify-content: start; gap: .4rem;")
            .And.NotContain(".partner-financial-lines > span { display: flex !important; align-items: baseline; justify-content: space-between;");
    }

    [Fact]
    public void PartnerWorkspaceUsesUnifiedRoleDerivedCategoriesAndNavigation()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml.cs");
        var details = ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Details.cshtml");
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");
        var script = ReadFileIfExists("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "partner-workspace.js");

        page.Should().Contain("@page \"{scope?}\"")
            .And.Contain("data-partner-category-tabs")
            .And.Contain("asp-route-category")
            .And.Contain("施工班组")
            .And.Contain("甲方/总包")
            .And.Contain("其他合作单位")
            .And.Contain("Editor.PreviousRoleType")
            .And.Contain("Url.Page(\"/Crews/Details\"")
            .And.Contain("id=\"@tableId\"")
            .And.Contain("data-default-role")
            .And.Contain("data-entity-label");

        model.Should().Contain("public const string CustomerScope = \"customers\";")
            .And.Contain("public const string CrewCategory = \"crews\";")
            .And.Contain("public const string CustomerCategory = \"customers\";")
            .And.Contain("public const string OtherCategory = \"other\";")
            .And.Contain("public string? Category")
            .And.Contain("CategorySummaries")
            .And.Contain("BusinessPartnerRoleType.CustomerOrGeneralContractor")
            .And.Contain("ApplyCategory(")
            .And.Contain("directorySynchronizer.SynchronizeAsync(null, cancellationToken)")
            .And.Contain("partnerService.ListForManagementAsync(null, null, cancellationToken)")
            .And.Contain("Editor.PreviousRoleType");

        script.Should().Contain("const defaultRole = Number.parseInt(page.dataset.defaultRole")
            .And.Contain("const entityLabel = page.dataset.entityLabel")
            .And.Contain("payload.roleType ?? defaultRole")
            .And.Contain("setField(\"PreviousRoleType\"");

        details.Should().Contain("Request.Query[\"ReturnUrl\"]")
            .And.Contain("Url.IsLocalUrl(requestedReturnUrl)")
            .And.Contain("isCustomerReturn")
            .And.Contain("href=\"@returnUrl\"")
            .And.Contain("@if (!isCustomerReturn)");

        layout.Split(">合作单位</span>", StringSplitOptions.None).Should().HaveCount(2);
        layout.Should().NotContain(">施工班组</span>")
            .And.NotContain(">甲方/总包</span>")
            .And.Contain("currentPage.StartsWith(\"/Crews\"")
            .And.Contain("asp-page=\"/Partners/Index\"");
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
