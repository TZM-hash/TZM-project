using System.Net;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngineeringManager.Tests.Web;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task LiveHealthEndpointReturnsOkWithoutDatabaseAccess()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyHealthEndpointReturnsServiceUnavailableForAnUnavailableDatabase()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1,1;Database=Unavailable;User Id=invalid;Password=invalid;TrustServerCertificate=True;Connect Timeout=1"));
    }
}
