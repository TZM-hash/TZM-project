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
            .And.Contain(".company-portfolio-grid")
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

        pageCss.Should().Contain(".equipment-workspace-layout { display: grid; grid-template-columns: minmax(230px, .32fr) minmax(0, 1fr);")
            .And.Contain(".equipment-table-wrap { overflow-x: hidden; }")
            .And.Contain(".equipment-table { width: 100%; table-layout: fixed;")
            .And.Contain("@media (max-width: 900px)")
            .And.Contain(".equipment-workspace-layout { grid-template-columns: 1fr; }")
            .And.Contain(".equipment-table-wrap { overflow-x: auto; }");

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
            .And.Contain("if (ownership === \"SelfOwned\" && lessor) lessor.value = \"\"")
            .And.Contain("if (ownership === \"Rented\" && owner) owner.value = \"\"")
            .And.Contain("page.querySelector(\".workbench-inline-filters\")")
            .And.Contain("[\"CompanyId\", page.dataset.companyId]")
            .And.Contain("[\"Unassigned\", page.dataset.unassigned === \"true\" ? \"true\" : \"\"]")
            .And.Contain("page.querySelector(\".workbench-inline-clear\")");
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
