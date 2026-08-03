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

        css.Should().Contain("body.theme-default")
            .And.Contain("body.theme-clear-glass")
            .And.Contain("body.theme-clear-glass .table-wrap > table th")
            .And.Contain("body.theme-clear-glass .equipment-list-toolbar--integrated .data-workbench-toolbar")
            .And.Contain(".option-card-grid--themes")
            .And.Contain("--app-primary-rgb: 37, 99, 235")
            .And.Contain("rgba(var(--app-primary-rgb), .2)")
            .And.Contain("body.table-density-compact .table-wrap > table th")
            .And.Contain("body.table-density-spacious .table-wrap > table th");
        css.Should().Contain("body.theme-lavender-cream")
            .And.Contain("--app-primary: #7653d6")
            .And.Contain(".option-preview--lavender")
            .And.Contain("body.theme-lavender-cream .app-sidebar")
            .And.Contain("body.theme-lavender-cream .data-table th");
        css.Should().Contain("body.motion-apple.ui-effects-high");
        css.Should().Contain("body.ui-effects-low");
        css.Should().Contain("body.ui-effects-medium");
        css.Should().Contain("@media (prefers-reduced-motion: reduce)");
        css.Should().Contain("backdrop-filter: blur(24px) saturate(170%)");
        css.Should().Contain("body.appearance-classic")
            .And.Contain("body.appearance-rounded-soft")
            .And.Contain("--appearance-card-radius: 22px")
            .And.Contain("--appearance-dialog-radius: 26px")
            .And.Contain("--appearance-control-radius: 14px")
            .And.Contain("--appearance-card-shadow")
            .And.Contain("--appearance-overlay-shadow");
        layout.Should().Contain("@displaySettings.AppearanceCssClass");
        js.Should().Contain("initThemePreview")
            .And.Contain("initAppearancePreview")
            .And.Contain("initTableDensityPreview")
            .And.Contain("initSidebar")
            .And.Contain("initEffects")
            .And.Contain("\"theme-lavender-cream\"")
            .And.Contain("\"appearance-rounded-soft\"")
            .And.Contain("meta[name=\"theme-color\"]")
            .And.Contain("\"table-density-spacious\"");
        layout.Should().Contain("type=\"module\"")
            .And.Contain("content=\"@displaySettings.ThemeColor\"");
    }

    [Fact]
    public void RoundedAppearanceCoversCardsDialogsControlsTablesMenusAndNavigation()
    {
        var css = ReadCss();

        css.Should().Contain("body.appearance-rounded-soft :is(")
            .And.Contain(".workbench-dialog")
            .And.Contain(".quick-edit-dialog")
            .And.Contain(".selection-dropdown-menu")
            .And.Contain(".column-manager-menu")
            .And.Contain(".project-workbook-export-popover")
            .And.Contain(".button")
            .And.Contain("input:not([type=\"checkbox\"])")
            .And.Contain("body.appearance-rounded-soft :is(.table-wrap")
            .And.Contain("body.appearance-rounded-soft .nav-link")
            .And.Contain("body.appearance-rounded-soft .app-sidebar")
            .And.Contain("body.appearance-rounded-soft .quick-edit-dialog");
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

    [Fact]
    public void SharedConflictNoticeRequiresExplicitRefreshInsteadOfOverwriting()
    {
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");
        var site = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");
        var component = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "components", "conflict-notice.js");

        layout.Should().Contain("data-conflict-notice").And.Contain("data-conflict-refresh");
        site.Should().Contain("./components/conflict-notice.js");
        component.Should().Contain("validation-summary-errors")
            .And.Contain("window.location.reload()")
            .And.NotContain("requestSubmit");
    }

    [Fact]
    public void PageNavigationDoesNotWaitForDeferredModules()
    {
        var site = ReadFile("src", "EngineeringManager.Web", "wwwroot", "js", "site.js");
        var layout = ReadFile("src", "EngineeringManager.Web", "Pages", "Shared", "_Layout.cshtml");

        site.Should().Contain("requestIdleCallback")
            .And.NotContain("await Promise.all(jobs)")
            .And.Contain("navigation-pending");
        layout.Should().Contain("data-navigation-pending");
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
