using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class ResponsiveUiAssetTests
{
    [Fact]
    public void ResponsiveAssetsProtectTouchTargetsTablesFormsAndMobileNavigation()
    {
        var css = ReadCss();
        var login = ReadFile("src", "EngineeringManager.Web", "Areas", "Identity", "Pages", "Account", "Login.cshtml");

        css.Should().Contain("@media (max-width: 760px)");
        css.Should().Contain("min-height: 44px");
        css.Should().Contain("overflow-x: auto");
        css.Should().Contain(".sticky-actions");
        css.Should().Contain(".detail-grid");
        login.Should().Contain("auth-page").And.Contain("auth-card");
    }

    [Fact]
    public void PrimaryFormsExposeResponsiveSectionsAndStickyActions()
    {
        var formPages = new[]
        {
            new[] { "src", "EngineeringManager.Web", "Pages", "Employees", "Create.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Companies", "Edit.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Equipment", "Edit.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Equipment", "Usage.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Equipment", "Settlement.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Finance", "Accounts.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Finance", "Entries", "Create.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Partners", "Create.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "StageResults", "Create.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "Equipment", "Offline.cshtml" },
            new[] { "src", "EngineeringManager.Web", "Pages", "StageResults", "Offline.cshtml" }
        };

        foreach (var page in formPages)
        {
            var markup = ReadFile(page);
            markup.Should().Contain("form-section", string.Join("/", page));
            markup.Should().Contain("sticky-actions", string.Join("/", page));
        }
    }

    [Fact]
    public void CompanyDetailsUsesResponsiveDetailGrid()
    {
        var markup = ReadFile("src", "EngineeringManager.Web", "Pages", "Companies", "Details.cshtml");

        markup.Should().Contain("detail-grid");
    }

    private static string ReadCss()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "EngineeringManager.Web", "wwwroot", "css");
        return string.Join('\n', Directory.EnumerateFiles(directory, "*.css", SearchOption.TopDirectoryOnly).Select(File.ReadAllText));
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EngineeringManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
