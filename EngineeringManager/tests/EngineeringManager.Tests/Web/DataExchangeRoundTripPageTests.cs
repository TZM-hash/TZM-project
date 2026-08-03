using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class DataExchangeRoundTripPageTests
{
    [Fact]
    public void DataExchangeHasDedicatedExportImportAndTaskRoutes()
    {
        var root = RepositoryRoot();
        var exportRazor = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "DataExchange", "Export.cshtml"));
        var importRazor = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "DataExchange", "Import.cshtml"));
        var tasksRazor = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "DataExchange", "Tasks.cshtml"));
        var indexModel = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "DataExchange", "Index.cshtml.cs"));

        exportRazor.Should().Contain("@page \"/DataExchange/Export\"")
            .And.Contain("asp-page=\"./Import\"")
            .And.Contain("asp-page-handler=\"ExportModules\"")
            .And.Contain("asp-page-handler=\"ExportProjectWorkbook\"");
        importRazor.Should().Contain("@page \"/DataExchange/Import\"")
            .And.Contain("asp-page-handler=\"PreviewImport\"")
            .And.Contain("asp-page-handler=\"PreviewProjectWorkbook\"")
            .And.Contain("下载错误报告 Excel");
        tasksRazor.Should().Contain("@page \"/DataExchange/Tasks\"")
            .And.Contain("HistoryPageSize")
            .And.Contain("asp-page-handler=\"DownloadExport\"")
            .And.Contain("最新任务在前");
        indexModel.Should().Contain("RedirectToPage(\"./Export\")");
    }

    [Fact]
    public void DataExchangeNavigationStartsAtExportRoute()
    {
        var root = RepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml"));
        layout.Should().Contain("asp-page=\"/DataExchange/Export\"");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
