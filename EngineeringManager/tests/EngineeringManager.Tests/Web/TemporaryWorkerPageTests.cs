using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class TemporaryWorkerPageTests
{
    [Fact]
    public void TemporaryWorkerPagesExposeOptionalIdentityHistoryAndSourceLinks()
    {
        var index = ReadFile("src", "EngineeringManager.Web", "Pages", "TemporaryWorkers", "Index.cshtml");
        var details = ReadFile("src", "EngineeringManager.Web", "Pages", "TemporaryWorkers", "Details.cshtml");

        index.Should().Contain("临时人员");
        index.Should().Contain("身份证号（选填）");
        index.Should().Contain("历史发放合计");
        details.Should().Contain("历次发放记录");
        details.Should().Contain("查看来源批次");
        details.Should().Contain("转为员工");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));
    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
