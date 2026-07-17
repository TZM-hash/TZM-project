using System.Net;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngineeringManager.Tests.Web;

public sealed class OfflineAssetsTests
{
    [Fact]
    public async Task OfflineScriptDefinesPartitionedStoresPhotoLimitsAndRetryQueue()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/js/offline-stage-results.js");
        var script = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        script.Should().Contain("indexedDB.open");
        script.Should().Contain("drafts");
        script.Should().Contain("photos");
        script.Should().Contain("queue");
        script.Should().Contain("metadata");
        script.Should().Contain("userId");
        script.Should().Contain("MAX_PHOTOS = 20");
        script.Should().Contain("MAX_EDGE = 1920");
        script.Should().Contain("MAX_PHOTO_BYTES = 3 * 1024 * 1024");
        script.Should().Contain("navigator.storage.estimate");
        script.Should().Contain("window.addEventListener('online'");
        script.Should().Contain("nextAttemptAt");
        script.Should().Contain("data-offline-conflict-panel");
        script.Should().Contain("clearUserData");
    }

    [Fact]
    public async Task ServiceWorkerExcludesSensitiveBusinessRoutesFromCaching()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var script = await client.GetStringAsync("/service-worker.js");

        script.Should().Contain("engineering-manager-shell-v3");
        script.Should().Contain("/js/components/data-table.js");
        script.Should().Contain("/js/components/charts.js");
        script.Should().Contain("SENSITIVE_PREFIXES");
        script.Should().Contain("'/api/'");
        script.Should().Contain("'/Finance'");
        script.Should().Contain("'/Payroll'");
        script.Should().Contain("'/DataExchange'");
        script.Should().Contain("request.method !== 'GET'");
        script.Should().Contain("cacheFirst");
        script.Should().Contain("networkFirst");
    }
}
