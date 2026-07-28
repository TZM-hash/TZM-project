using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class LocalWebStartupScriptTests
{
    [Fact]
    public void LocalStartupScriptSafelyReplacesStaleServiceAndWaitsForReadiness()
    {
        var scriptPath = Path.Combine(RepositoryRoot(), "scripts", "start-local-web.ps1");

        File.Exists(scriptPath).Should().BeTrue();
        var script = File.ReadAllText(scriptPath);

        script.Should().Contain("$ErrorActionPreference = 'Stop'")
            .And.Contain("Get-NetTCPConnection")
            .And.Contain("EngineeringManager.Web")
            .And.Contain("Stop-Process")
            .And.Contain("--no-launch-profile")
            .And.Contain("--configuration")
            .And.Contain("/health/ready")
            .And.Contain("Start-Process")
            .And.Contain("RedirectStandardOutput")
            .And.Contain("RedirectStandardError");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
