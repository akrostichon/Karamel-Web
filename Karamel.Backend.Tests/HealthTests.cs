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
}
