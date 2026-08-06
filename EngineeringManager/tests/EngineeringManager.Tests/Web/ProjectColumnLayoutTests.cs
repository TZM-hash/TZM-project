using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectColumnLayoutTests
{
    [Fact]
    public void ProjectListKeepsContactAfterContractorAndUsesEqualProgressColumnWidths()
    {
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");
        var dataTable = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");

        model.IndexOf("new(\"general_contractor\", \"总包单位\")", StringComparison.Ordinal)
            .Should().BeLessThan(model.IndexOf("new(\"general_contractor_contact\", \"总包联系人 / 电话\")", StringComparison.Ordinal));
        css.Should().Contain("#projects-table [data-column-key=\"collection_progress\"], #projects-table [data-column-key=\"payment_progress\"], #projects-table [data-column-key=\"invoice_progress\"] { width: 11rem; min-width: 11rem; max-width: 11rem; }");
        dataTable.Should().Contain("project-contact-after-contractor-v1")
            .And.Contain("general_contractor_contact")
            .And.Contain("columnOrderMigrations");
    }

    [Fact]
    public void ProjectProgressDetailsStayInsideTheirColumns()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        css.Should().Contain("#projects-table [data-column-key=\"collection_progress\"] .mini-progress, #projects-table [data-column-key=\"payment_progress\"] .mini-progress, #projects-table [data-column-key=\"invoice_progress\"] .mini-progress { width: 100%; min-width: 0; max-width: 100%; }")
            .And.Contain("#projects-table [data-column-key=\"collection_progress\"] .mini-progress-values, #projects-table [data-column-key=\"payment_progress\"] .mini-progress-values, #projects-table [data-column-key=\"invoice_progress\"] .mini-progress-values { display: block; min-width: 0; max-width: 100%; overflow-wrap: anywhere; white-space: normal; }");
    }

    [Fact]
    public void ProjectListUsesACompactNameColumnAndRoomierFollowingColumns()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        css.Should().Contain("#projects-table [data-column-key=\"project_name\"] { width: 28rem; min-width: 28rem; max-width: 28rem; }")
            .And.Contain("#projects-table [data-column-key=\"project_name\"] > a { display: block; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }")
            .And.Contain("#projects-table [data-column-key=\"current_project_amount\"] { width: 9rem; min-width: 9rem; max-width: 9rem; }")
            .And.Contain("#projects-table [data-column-key=\"general_contractor\"] { width: 12rem; max-width: 12rem; }")
            .And.Contain("#projects-table [data-column-key=\"general_contractor_contact\"] { width: 11.5rem; min-width: 11.5rem; max-width: 11.5rem; white-space: normal; }")
            .And.Contain("#projects-table [data-column-key=\"collection_progress\"], #projects-table [data-column-key=\"payment_progress\"], #projects-table [data-column-key=\"invoice_progress\"] { width: 11rem; min-width: 11rem; max-width: 11rem; }");
    }

    [Fact]
    public void ProjectTableUsesVisibleColumnCombinationToAvoidColumnCompression()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        css.Should().Contain("#projects-table { width: max-content; min-width: 100%; }");
    }

    [Fact]
    public void ProjectContactListUsesTheFullVisibleContactColumnWidth()
    {
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        css.Should().Contain(".project-contact-list { display: grid; gap: .1rem; min-width: 0; width: 100%; max-width: 100%; }");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
