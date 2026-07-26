using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ResponsiveUiAssetTests
{
    [Fact]
    public void ResponsiveAssetsProtectTouchTargetsTablesFormsAndMobileNavigation()
    {
        var css = ReadCss();
        var login = ReadFile("src", "EngineeringManager.Web", "Areas", "Identity", "Pages", "Account", "Login.cshtml");

        css.Should().Contain("@media (max-width: 760px)");
        css.Should().Contain("min-height: 44px");
        css.Should().Contain("overflow-x: auto");
        css.Should().Contain(".sticky-actions");
        css.Should().Contain(".detail-grid");
        css.Should().Contain(".chart-data-table.sr-only");
        css.Should().Contain("contain: strict");
        login.Should().Contain("auth-page").And.Contain("auth-card");
    }

    [Fact]
    public void PrimaryFormsExposeResponsiveSectionsAndStickyActions()
    {
        var formPages = new[]
        {
            new[] { "src", "EngineeringManager.Web", "Pages", "Employees", "Create.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Companies", "Edit.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Equipment", "Edit.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Equipment", "Usage.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Equipment", "Settlement.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Finance", "Accounts.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Finance", "Entries", "Create.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Partners", "Create.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "StageResults", "Create.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Equipment", "Offline.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "StageResults", "Offline.cshtml" }
        };

        foreach (var page in formPages)
        {
            var markup = ReadFile(page);
            markup.Should().Contain("form-section", string.Join("/", page));
            markup.Should().Contain("sticky-actions", string.Join("/", page));
        }
    }

    [Fact]
    public void CompanyDetailsUsesResponsiveDetailGrid()
    {
        var markup = ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Details.cshtml");
        var css = ReadCss();

        markup.Should().Contain("detail-grid").And.Contain("company-detail-full-grid");
        css.Should().Contain(".company-detail-full-grid");
    }

    [Fact]
    public void CompanyOverviewGivesTheCompactListMostOfTheHorizontalWorkspace()
    {
        var markup = ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Index.cshtml");
        var css = ReadCss();

        markup.Should().Contain("company-workspace--overview")
            .And.Contain("company-dashboard-stack")
            .And.Contain("company-list-panel");
        css.Should().Contain(".company-workspace--overview")
            .And.Contain(".company-portfolio-grid { grid-template-columns: minmax(22rem, .5fr) minmax(0, 1.5fr); align-items: stretch; }")
            .And.Contain(".company-dashboard-stack { display: grid; min-width: 0; grid-template-rows: auto minmax(0, 1fr); align-content: stretch;")
            .And.Contain(".company-dashboard-stack--single { align-self: stretch; grid-template-rows: minmax(0, 1fr); }")
            .And.Contain("grid-template-columns: minmax(22rem, .5fr) minmax(0, 1.5fr)")
            .And.Contain(".company-category-table-wrap { max-height: none; overflow: visible; }")
            .And.Contain(".company-list-panel { display: flex; min-width: 0; align-self: stretch;");
    }

    [Fact]
    public void CompanyListDensityControlsAreNotOverriddenByAFixedRowHeight()
    {
        var pageCss = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var tableScript = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");

        tableScript.Should().Contain("table.classList.add(`row-spacing-${value}`)");
        pageCss.Should().NotContain(".company-list-panel .data-table th, .company-list-panel .data-table td { height: 2rem;");
    }

    [Fact]
    public void CompanyInlineEditorsStayInTheirDisplayedCellsAndCategoryInputsUseEqualColumns()
    {
        var pageCss = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        pageCss.Should().Contain(".company-category-create-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr));")
            .And.Contain(".company-workspace--details .inline-edit-shell [data-inline-edit-control].inline-cell-control:not([hidden])")
            .And.Contain(".company-category-panel [data-inline-edit-control].inline-cell-control:not([hidden])")
            .And.Contain("position: static;");
    }

    [Fact]
    public void EquipmentWorkspaceKeepsTheDesktopListCompactAndStacksOnNarrowScreens()
    {
        var pageCss = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        pageCss.Should().Contain(".equipment-workspace-layout { display: grid; grid-template-columns: minmax(260px, .32fr) minmax(0, 1fr); gap: .85rem; align-items: stretch;")
            .And.Contain(".equipment-workspace-summary { position: sticky; top: 5rem; display: grid; grid-template-rows: auto auto auto auto minmax(0, 1fr); gap: .72rem; min-height: 0; max-height: none;")
            .And.Contain(".equipment-table-wrap { overflow-x: auto; }")
            .And.Contain(".equipment-table { width: 100%; table-layout: fixed;")
            .And.Contain("min-width: 76rem;")
            .And.Contain("-webkit-line-clamp: 2;")
            .And.Contain(".equipment-table td > span:not(.pill)")
            .And.Contain(".equipment-table td[data-column-key=\"status\"] .pill")
            .And.Contain(".equipment-table th:nth-child(9) { width: 16%; }")
            .And.Contain(".equipment-row-actions { flex-wrap: nowrap;")
            .And.Contain("@media (max-width: 900px)")
            .And.Contain(".equipment-workspace-layout { grid-template-columns: 1fr; }")
            .And.Contain(".equipment-table-wrap { overflow-x: auto; }");

        pageCss.Should().NotContain(".equipment-workspace-summary { position: sticky; top: 5rem; display: grid; grid-template-rows: auto auto auto auto minmax(0, 1fr); gap: .72rem; min-height: calc(100vh - 5.5rem);");

        pageCss.Should().Contain("var(--app-border)")
            .And.Contain("var(--app-surface)")
            .And.NotContain(".equipment-company-filter a { flex: 0 0 auto; padding: .48rem .78rem; border: 1px solid var(--line)");
    }

    [Fact]
    public void EquipmentWorkspaceScriptUsesDialogsAndSynchronizesOwnershipFields()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "equipment-workspace.js");

        script.Should().Contain("dialog.showModal()")
            .And.Contain("field(\"OwnershipType\")?.addEventListener(\"change\", syncOwnership)")
            .And.Contain("selfOwned.hidden = ownership !== \"SelfOwned\"")
            .And.Contain("rented.hidden = ownership !== \"Rented\"")
            .And.Contain("if (ownership !== \"SelfOwned\" && owner) owner.value = \"\"")
            .And.Contain("if (ownership !== \"Rented\" && lessor) lessor.value = \"\"")
            .And.Contain("page.querySelector(\".workbench-inline-filters\")")
            .And.Contain("[\"CompanyId\", page.dataset.companyId]")
            .And.Contain("const companySelect = form.querySelector('[name=\"CompanyId\"]')")
            .And.Contain("companySelect?.addEventListener(\"change\", () => form.requestSubmit())")
            .And.Contain("[data-equipment-delete-open]")
            .And.Contain("initAttachmentPreview()")
            .And.Contain("page.querySelector(\".workbench-inline-clear\")");
    }

    [Fact]
    public void CompanyCertificateWorkspaceUsesAutomaticFilteringCopyRulesAndUnifiedReminderColors()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "company-certificate-workspace.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        script.Should().Contain("form.querySelector('[name=\"CompanyId\"]')")
            .And.Contain("addEventListener(\"change\", () => form.requestSubmit())")
            .And.Contain("setField(\"CertificateNumber\", copy ? \"\" : payload.certificateNumber)")
            .And.Contain("setField(\"IssuedOn\", copy ? \"\" : payload.issuedOn)")
            .And.Contain("setField(\"ExpiresOn\", copy ? \"\" : payload.expiresOn)")
            .And.Contain("initAttachmentPreview()");

        css.Should().Contain(".company-certificate-workspace-layout")
            .And.Contain("align-items: stretch")
            .And.Contain(".company-certificate-list-toolbar .data-workbench-toolbar { min-width: 0; flex-wrap: wrap;")
            .And.Contain(".company-certificate-list-toolbar .workbench-inline-filters { flex-wrap: wrap;")
            .And.Contain(".company-certificate-workspace-table { width: 100%; min-width: 68rem;")
            .And.Contain(".company-certificate-workspace-table th:nth-child(7) { width: 28%; }")
            .And.Contain(".certificate-status--info { background: #fef9c3; color: #a16207;")
            .And.Contain(".certificate-status--warning { background: #ffedd5; color: #c2410c;")
            .And.Contain(".certificate-status--critical { background: #fee2e2; color: #b91c1c;")
            .And.Contain(".certificate-status--expired { background: #7f1d1d; color: #fff;");
    }

    [Fact]
    public void ReminderCenterUsesTheSameLocalizedSeverityPresentationAsCertificates()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Reminders", "Index.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Reminders", "Index.cshtml.cs");

        page.Should().Contain("IndexModel.SeverityLabel(item.Severity)")
            .And.Contain("certificate-status--@IndexModel.SeverityClass(item.Severity)");
        model.Should().Contain("ReminderSeverity.Info => \"轻度提醒\"")
            .And.Contain("ReminderSeverity.Warning => \"中度提醒\"")
            .And.Contain("ReminderSeverity.Critical => \"高度提醒\"");
    }

    [Fact]
    public void EquipmentAndCompanyDialogsUseUnifiedMacStyleAndSemanticActions()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var equipment = ReadFile("src", "EngineeringManager.Web", "Pages", "Equipment", "Index.cshtml");
        var companies = ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Index.cshtml");

        css.Should().Contain(".mac-window-dialog")
            .And.Contain(".mac-window-controls")
            .And.Contain(".action-button--view")
            .And.Contain(".action-button--edit")
            .And.Contain(".action-button--copy")
            .And.Contain(".action-button--usage")
            .And.Contain(".action-button--certificate")
            .And.Contain(".action-button--delete");
        equipment.Should().Contain("mac-window-dialog").And.Contain("mac-window-controls");
        companies.Should().Contain("mac-window-dialog").And.Contain("mac-window-controls");
    }

    [Fact]
    public void CompanyDialogsUseTheEquipmentHorizontalLayoutAndStackOnMobile()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Index.cshtml");
        var details = ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Details.cshtml");
        var dialogTags = System.Text.RegularExpressions.Regex.Matches(
                string.Join('\n', index, details),
                "<dialog\\b[^>]*>",
                System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(match => match.Value)
            .ToArray();

        dialogTags.Should().HaveCount(8)
            .And.OnlyContain(tag => tag.Contains("mac-window-dialog", StringComparison.Ordinal));
        dialogTags.Where(tag => !tag.Contains("attachment-preview-dialog", StringComparison.Ordinal))
            .Should().OnlyContain(tag => tag.Contains("company-dialog", StringComparison.Ordinal));

        css.Should().Contain(".company-dialog { width: min(68rem, calc(100vw - 2rem));")
            .And.Contain(".company-dialog > form { display: grid; grid-template-rows: auto minmax(0, 1fr) auto;")
            .And.Contain(".company-dialog-form-grid > .form-section { grid-column: auto;")
            .And.Contain(".company-view-dialog-body { grid-template-columns: repeat(3, minmax(0, 1fr));")
            .And.Contain(".mac-window-dialog .workbench-dialog-heading .mac-window-controls { display: inline-flex;")
            .And.Contain(".mac-window-controls i { display: block;")
            .And.Contain("@media (max-width: 680px)")
            .And.Contain(".company-dialog-form-grid, .company-view-dialog-body { grid-template-columns: 1fr; }");
    }

    [Fact]
    public void EquipmentUploadAndDownloadHandlersEnforceWebBoundaryChecks()
    {
        var pageModel = ReadFile("src", "EngineeringManager.Web", "Pages", "Equipment", "Index.cshtml.cs");

        pageModel.Should().Contain("QualificationAttachmentFile.Length is <= 0 or > CertificateAttachmentUpload.MaxSizeBytes")
            .And.Contain("catch (KeyNotFoundException)")
            .And.Contain("return NotFound()");
    }

    private static string ReadCss()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "css");
        return string.Join('\n', Directory.EnumerateFiles(directory, "*.css", SearchOption.TopDirectoryOnly).Select(File.ReadAllText));
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
