using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ConstructionCrewPageTests
{
    [Fact]
    public void CrewPagesExposeRosterProjectAndPayrollHistory()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Index.cshtml");
        var details = ReadFile("src", "EngineeringManager.Web", "Pages", "Crews", "Details.cshtml");

        index.Should().Contain("施工班组管理");
        index.Should().Contain("当前人员");
        index.Should().Contain("累计代发工程款");
        details.Should().Contain("人员名册");
        details.Should().Contain("民工工资发放记录");
        details.Should().Contain("查看来源批次");
        details.Should().Contain("asp-page-handler=\"AddWorker\"");
        details.Should().Contain("asp-page-handler=\"TransferWorker\"");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));
    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
