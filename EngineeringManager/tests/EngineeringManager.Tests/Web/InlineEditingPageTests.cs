using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class InlineEditingPageTests
{
    [Fact]
    public void ExistingQuickEditPagesKeepMainEditorsInlineWithOnlyTheQuantityNotesDialogException()
    {
        var projectDetails = ReadPage("Projects", "Details.cshtml");
        var pages = new[]
        {
            projectDetails,
            ReadPage("Companies", "Details.cshtml"),
            ReadPage("Equipment", "Details.cshtml"),
            ReadPage("Partners", "Index.cshtml")
        };

        pages.Should().OnlyContain(page => page.Contains("data-inline-cell-edit", StringComparison.Ordinal));
        pages.Skip(1).Should().OnlyContain(page => !page.Contains("<dialog", StringComparison.OrdinalIgnoreCase));
        projectDetails.Should().Contain("<dialog class=\"workbench-dialog quantity-notes-dialog\"");
        projectDetails.Should().Contain("data-attachment-preview-dialog");
        (projectDetails.Split("<dialog", StringSplitOptions.None).Length - 1).Should().Be(2);
        pages.Should().OnlyContain(page => !page.Contains("data-quick-edit-dialog", StringComparison.Ordinal));
    }

    [Fact]
    public void InlineEditorScriptSupportsSectionAndTableEditing()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "quick-edit.js");

        script.Should().Contain("initInlineEditors")
            .And.Contain("data-inline-edit-open")
            .And.Contain("data-inline-edit-control")
            .And.Contain("data-inline-edit-kind-select")
            .And.NotContain("showModal");
    }

    [Fact]
    public void InlineCellEditingKeepsTableGeometryStable()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "quick-edit.js");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "components.css");

        script.Should().Contain("focus({ preventScroll: true })");
        css.Should().Contain(".inline-edit-shell [data-inline-edit-control].inline-cell-control:not([hidden]) { position: absolute")
            .And.Contain(".inline-edit-shell td[data-column-key=\"actions\"] .table-action-buttons { flex-wrap: nowrap; }");
    }

    [Fact]
    public void EmployeeAndPartnerActionsUseCompactButtonsAndEmployeeTabsAreButtonShaped()
    {
        var employee = ReadPage("Employees", "Index.cshtml");
        var partner = ReadPage("Partners", "Index.cshtml");
        var subNavigation = ReadPage("Employees", "_EmployeeSubNavigation.cshtml");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "components.css");

        employee.Should().Contain("table-action-buttons--compact");
        partner.Should().Contain("table-action-buttons--compact");
        subNavigation.Should().Contain("page-tabs--buttons")
            .And.Contain("button--secondary")
            .And.Contain("is-active");
        css.Should().Contain(".table-action-buttons--compact .button")
            .And.Contain(".page-tabs--buttons .button");
    }

    [Fact]
    public void FinanceProjectScopedSelectsLoadTheirClientSideFilteringModule()
    {
        var siteScript = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

        siteScript.Should().Contain("[data-finance-project-select]");
    }

    [Fact]
    public void RowEditorsAreNotNestedInsideAnotherInlineEditor()
    {
        var employees = ReadPage("Employees", "Index.cshtml");
        var partners = ReadPage("Partners", "Index.cshtml");

        employees.Should().NotContain("data-inline-cell-edit")
            .And.NotContain("data-inline-edit=\"employee-list\"");
        partners.Should().Contain("data-inline-edit=\"partner-@partner.Id\"")
            .And.NotContain("data-inline-edit=\"partner-list\"");
    }

    [Fact]
    public void JumpPagesExposeSmartBackAndActionButtonsShareOneSize()
    {
        var layout = ReadPage("Shared", "_Layout.cshtml");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "components.css");

        layout.Should().Contain("data-smart-back")
            .And.Contain("返回上级");
        css.Should().Contain(".button--action")
            .And.Contain("min-width:");
    }

    private static string ReadPage(params string[] parts)
    {
        var pathParts = new List<string> { "src", "EngineeringManager.Web", "Pages" };
        pathParts.AddRange(parts);
        return ReadFile(pathParts.ToArray());
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
