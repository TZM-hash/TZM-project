using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class InlineEditingPageTests
{
    [Fact]
    public void ExistingQuickEditPagesUseInlineEditorsInsteadOfDialogs()
    {
        var pages = new[]
        {
            ReadPage("Projects", "Details.cshtml"),
            ReadPage("Companies", "Details.cshtml"),
            ReadPage("Equipment", "Details.cshtml"),
            ReadPage("Employees", "Index.cshtml"),
            ReadPage("Partners", "Index.cshtml")
        };

        pages.Should().OnlyContain(page => page.Contains("data-inline-cell-edit", StringComparison.Ordinal));
        pages.Should().OnlyContain(page => !page.Contains("<dialog", StringComparison.OrdinalIgnoreCase));
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

        employees.Should().Contain("data-inline-edit=\"employee-@item.Id\"")
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
