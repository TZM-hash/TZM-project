using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectContactColumnTests
{
    [Fact]
    public void ProjectListRendersLongGeneralContractorContactsAsWrappedRowsInACompactColumn()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("project-contact-list")
            .And.Contain("StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries")
            .And.Contain("data-column-key=\"general_contractor_contact\"");
        css.Should().Contain("#projects-table [data-column-key=\"general_contractor_contact\"] { width: 11.5rem; min-width: 11.5rem; max-width: 11.5rem; white-space: normal; }")
            .And.Contain(".project-contact-list { display: grid; gap: .1rem; min-width: 0; width: 100%; max-width: 100%; }")
            .And.Contain(".project-contact-list > span { display: block; overflow-wrap: anywhere; white-space: normal; }");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
