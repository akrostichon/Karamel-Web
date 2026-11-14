using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Karamel.Backend.Tests;

public class RateLimitingTests : IClassFixture<TestServerFactory>
{
    private readonly TestServerFactory _factory;

    public RateLimitingTests(TestServerFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SessionCreate_IsRateLimited_WhenEnabled()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:SessionCreatePerMinute", "2");
        }).CreateClient();

        var request = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };

        var first = await client.PostAsJsonAsync("/api/sessions", request);
        var second = await client.PostAsJsonAsync("/api/sessions", request);
        var third = await client.PostAsJsonAsync("/api/sessions", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("60", third.Headers.GetValues("Retry-After").Single());

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task XForwardedFor_PartitionsRateLimits()
    {
        var clientA = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:SessionCreatePerMinute", "1");
        }).CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");

        var clientB = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:SessionCreatePerMinute", "1");
        }).CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.2");

        var request = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };

        var first = await clientA.PostAsJsonAsync("/api/sessions", request);
        var second = await clientB.PostAsJsonAsync("/api/sessions", request);
        var third = await clientB.PostAsJsonAsync("/api/sessions", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
