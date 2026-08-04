using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectDetailsNavigationTests
{
    [Fact]
    public void ProjectDetailsExposeAdjacentProjectNavigationInTheHeading()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml");
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Details.cshtml.cs");
        var css = ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", "pages.css");

        page.Should().Contain("project-detail-navigation")
            .And.Contain("上一页")
            .And.Contain("下一页")
            .And.Contain("Model.PreviousProjectId")
            .And.Contain("Model.NextProjectId");
        model.Should().Contain("ProjectNavigationResolver.Resolve")
            .And.Contain("SortKey: \"ProjectNumber\"")
            .And.Contain("SortDescending: true");
        css.Should().Contain(".project-detail-navigation");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
