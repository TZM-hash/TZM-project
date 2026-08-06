using System.Net;
using System.Net.Http.Headers;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngineeringManager.Tests.Web;

public sealed class StaticAssetCompressionTests
{
    [Fact]
    public async Task GzipNegotiationReturnsTheStaticAssetBody()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/css/base.css");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeEmpty();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/css");
    }
}
