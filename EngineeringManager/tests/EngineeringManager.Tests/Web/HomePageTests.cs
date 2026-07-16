using System.Net;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngineeringManager.Tests.Web;

public sealed class HomePageTests
{
    [Fact]
    public async Task HomePageUsesChineseResponsiveShellWithoutExternalCdn()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        var manifest = await client.GetStringAsync("/manifest.webmanifest");
        var serviceWorker = await client.GetStringAsync("/service-worker.js");
        var siteScript = await client.GetStringAsync("/js/site.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("工程项目经营管理系统");
        html.Should().Contain("name=\"viewport\"");
        html.Should().Contain("/css/base.");
        html.Should().NotContain("https://cdn.");
        html.Should().Contain("/manifest.");
        manifest.Should().Contain("工程项目经营管理系统");
        serviceWorker.Should().Contain("engineering-manager-shell-v1");
        siteScript.Should().Contain("/service-worker.js");
    }
}
