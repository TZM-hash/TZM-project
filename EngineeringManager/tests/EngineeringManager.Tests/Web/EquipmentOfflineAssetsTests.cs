using System.Net;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngineeringManager.Tests.Web;

public sealed class EquipmentOfflineAssetsTests
{
    [Fact]
    public async Task EquipmentOfflineScriptUsesPartitionedDraftPhotoQueueAndRetryPolicies()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/js/offline-equipment.js");
        var script = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        script.Should().Contain("indexedDB.open");
        script.Should().Contain("equipmentDrafts");
        script.Should().Contain("equipmentPhotos");
        script.Should().Contain("equipmentQueue");
        script.Should().Contain("userId");
        script.Should().Contain("MAX_PHOTOS = 20");
        script.Should().Contain("MAX_PHOTO_BYTES = 3 * 1024 * 1024");
        script.Should().Contain("nextAttemptAt");
        script.Should().Contain("window.addEventListener('online'");
        script.Should().Contain("clearUserData");
        script.Should().NotContain("totalAmount");
        script.Should().NotContain("payable");
    }

    [Fact]
    public async Task ServiceWorkerCachesEquipmentOfflineShellButExcludesFinancialEquipmentRoutes()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var script = await client.GetStringAsync("/service-worker.js");
        script.Should().Contain("/Equipment/Offline");
        script.Should().Contain("/js/offline-equipment.js");
        script.Should().Contain("/Equipment/Settlement");
    }
}
