using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Karamel.Backend.Tests;

public class HealthTests : IClassFixture<TestServerFactory>
{
    private readonly TestServerFactory _factory;

    public HealthTests(TestServerFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var client = _factory.CreateDefaultClient();
        var resp = await client.GetAsync("/health");
        resp.EnsureSuccessStatusCode();
        var content = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task HealthEndpoint_SetsSecurityHeaders()
    {
        var client = _factory.CreateDefaultClient();
        var resp = await client.GetAsync("/health");
        resp.EnsureSuccessStatusCode();

        Assert.True(resp.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeValues));
        Assert.Equal("nosniff", contentTypeValues.Single());

        Assert.True(resp.Headers.TryGetValues("Referrer-Policy", out var referrerValues));
        Assert.Equal("no-referrer", referrerValues.Single());
    }
}
