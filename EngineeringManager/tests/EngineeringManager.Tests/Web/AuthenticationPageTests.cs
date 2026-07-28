using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class AuthenticationPageTests
{
    [Fact]
    public void LoginPasswordFieldCanBeShownAndHidden()
    {
        var login = ReadFile("src", "EngineeringManager.Web", "Areas", "Identity", "Pages", "Account", "Login.cshtml");
        var script = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");

        login.Should().Contain("data-password-input")
            .And.Contain("data-password-toggle")
            .And.Contain("显示密码");
        script.Should().Contain("initPasswordVisibility")
            .And.Contain("[data-password-toggle]")
            .And.Contain("input.type = revealed ? \"text\" : \"password\"");
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
