using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class UnifiedDetailsDialogTests
{
    [Fact]
    public void WorkspaceViewDialogsUseSharedSectionedDetailPresentation()
    {
        var pages = new[]
        {
            ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Index.cshtml"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "_EmployeeEditor.cshtml"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Payroll", "Index.cshtml"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Partners", "Index.cshtml"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Index.cshtml"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Certificates", "Index.cshtml"),
            ReadFile("src", "EngineeringManager.Web", "Pages", "Equipment", "Index.cshtml")
        };

        pages.Should().OnlyContain(page => page.Contains("entity-details-body", StringComparison.Ordinal));
        pages.Should().OnlyContain(page => page.Contains("entity-detail-section", StringComparison.Ordinal));

        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        css.Should().Contain(".entity-details-body")
            .And.Contain(".entity-detail-section")
            .And.Contain(".entity-detail-section-heading")
            .And.Contain(".entity-detail-field-grid")
            .And.Contain("border-right: 1px solid #d8e2ee")
            .And.Contain("border-bottom: 1px solid #d8e2ee")
            .And.Contain("@media (max-width: 680px)");
    }

    [Fact]
    public void CompanyViewRendererBuildsBusinessSectionsInsteadOfAFlatFieldList()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "pages", "company-workspace.js");

        script.Should().Contain("const sections =")
            .And.Contain("entity-detail-section")
            .And.Contain("entity-detail-section-heading")
            .And.Contain("entity-detail-field-grid")
            .And.Contain("基本资料")
            .And.Contain("工商与联系资料")
            .And.Contain("开票与备注");
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
