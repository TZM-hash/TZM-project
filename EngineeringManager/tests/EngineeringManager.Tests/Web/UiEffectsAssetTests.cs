using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class UiEffectsAssetTests
{
    private static readonly string[] CssFiles = ["base.css", "components.css", "pages.css", "themes.css"];

    [Fact]
    public void AssetsContainConfirmedThemesEffectsAndReducedMotion()
    {
        var css = ReadCss();
        var js = ReadJavaScript();
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");

        css.Should().Contain("body.theme-clear-glass");
        css.Should().Contain("body.motion-apple.ui-effects-high");
        css.Should().Contain("body.ui-effects-low");
        css.Should().Contain("body.ui-effects-medium");
        css.Should().Contain("@media (prefers-reduced-motion: reduce)");
        css.Should().Contain("backdrop-filter: blur(24px) saturate(170%)");
        js.Should().Contain("initThemePreview").And.Contain("initSidebar").And.Contain("initEffects");
        layout.Should().Contain("type=\"module\"");
    }

    [Fact]
    public void FontsUseOnlyLocalCrossPlatformFallbackStacks()
    {
        var css = ReadCss();

        css.Should().Contain("body.font-microsoft-yahei");
        css.Should().Contain("body.font-microsoft-jhenghei");
        css.Should().Contain("body.font-chinese-serif");
        css.Should().Contain("body.font-chinese-kai");
        css.Should().Contain("PingFang SC").And.Contain("Noto Sans CJK SC");
        css.Should().NotContain("fonts.googleapis.com");
    }

    private static string ReadCss() => string.Join('\n', CssFiles
        .Select(file => ReadFile("src", "EngineeringManager.Web", "wwwroot", "css", file)));

    private static string ReadJavaScript()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "js");
        return string.Join('\n', Directory.EnumerateFiles(directory, "*.js", SearchOption.AllDirectories).Select(File.ReadAllText));
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
