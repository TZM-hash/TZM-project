using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ProjectListExportPageTests
{
    [Fact]
    public void ProjectPagePostsCurrentPageColumnsToThePageFormatExporter()
    {
        var model = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml.cs");
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "Index.cshtml");
        var export = ReadFile("src", "EngineeringManager.Web", "Pages", "Projects", "_ProjectWorkbookExport.cshtml");

        model.Should().Contain("ExportColumns { get; set; }")
            .And.Contain("ProjectListColumns: ExportColumns");
        page.Should().Contain("form=\"projects-table-workbook-export-form\"");
        export.Should().Contain("name=\"ExportColumns\"")
            .And.Contain("data-export-column-key=\"@column.Key\"")
            .And.Contain("column.Key != \"actions\"")
            .And.NotContain("data-export-column-key=\"actions\"");
    }

    [Fact]
    public void DataTableSynchronizesExportInputsWithColumnVisibilityAndOrder()
    {
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "data-table.js");

        script.Should().Contain("[data-project-export-scope]")
            .And.Contain("data-export-column-key")
            .And.Contain("exportInputs.sort")
            .And.Contain("input.disabled = byKey.get(input.dataset.exportColumnKey)?.visible === false");
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
