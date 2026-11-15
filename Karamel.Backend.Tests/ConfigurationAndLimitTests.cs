using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Karamel.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Karamel.Backend.Tests;

public class ConfigurationTests
{
    [Fact]
    public void CorsOriginParser_Production_RequiresConfiguredOrigins()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var environment = new TestHostEnvironment { EnvironmentName = "Production" };

        var ex = Assert.Throws<InvalidOperationException>(() => CorsOriginParser.Parse(config, environment));
        Assert.Contains("Cors:AllowedOrigins", ex.Message);
    }

    [Fact]
    public void CorsOriginParser_NormalizesConfiguredOrigins()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://example.com/",
                ["Cors:AllowedOrigins:1"] = "https://example.com",
                ["Cors:AllowedOrigins:2"] = " http://localhost:5000/ ",
                ["Cors:AllowedOrigins:3"] = ""
            })
            .Build();

        var environment = new TestHostEnvironment { EnvironmentName = "Production" };

        var origins = CorsOriginParser.Parse(config, environment);

        Assert.Equal(new[] { "https://example.com", "http://localhost:5000" }, origins);
    }
}

public class BackendHardeningLimitTests : IClassFixture<TestServerFactory>
{
    private readonly TestServerFactory _factory;

    public BackendHardeningLimitTests(TestServerFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BulkUpsert_Rejects_When_MaxSongsPerUpload_Is_Exceeded()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Karamel:MaxSongsPerUpload", "2");
        }).CreateDefaultClient();

        var createResp = await client.PostAsJsonAsync("/api/sessions", new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<CreateResponse>();
        Assert.NotNull(created);

        client.DefaultRequestHeaders.Add("X-Link-Token", created!.linkToken);

        var songs = Enumerable.Range(0, 3)
            .Select(i => new { id = Guid.NewGuid(), artist = $"Artist {i}", title = $"Title {i}", metadataJson = (string?)null })
            .ToArray();

        var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{created.Id}/library/bulk", songs);
        Assert.Equal(HttpStatusCode.BadRequest, uploadResp.StatusCode);
        var body = await uploadResp.Content.ReadAsStringAsync();
        Assert.Contains("Too many songs", body);
    }

    [Fact]
    public async Task BulkUpsert_Rejects_When_MaxSongsPerSession_Is_Exceeded()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Karamel:MaxSongsPerSession", "2");
        }).CreateDefaultClient();

        var createResp = await client.PostAsJsonAsync("/api/sessions", new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<CreateResponse>();
        Assert.NotNull(created);

        client.DefaultRequestHeaders.Add("X-Link-Token", created!.linkToken);

        var firstBatch = new[]
        {
            new { id = Guid.NewGuid(), artist = "Artist 1", title = "Title 1", metadataJson = (string?)null },
            new { id = Guid.NewGuid(), artist = "Artist 2", title = "Title 2", metadataJson = (string?)null }
        };

        var firstUpload = await client.PostAsJsonAsync($"/api/sessions/{created.Id}/library/bulk", firstBatch);
        Assert.Equal(HttpStatusCode.Accepted, firstUpload.StatusCode);

        var secondUpload = await client.PostAsJsonAsync($"/api/sessions/{created.Id}/library/bulk",
            new[] { new { id = Guid.NewGuid(), artist = "Artist 3", title = "Title 3", metadataJson = (string?)null } });

        Assert.Equal(HttpStatusCode.BadRequest, secondUpload.StatusCode);
        var body = await secondUpload.Content.ReadAsStringAsync();
        Assert.Contains("Library too large", body);
    }

    [Fact]
    public async Task CreateSession_Rejects_When_ActiveSessionLimit_Is_Reached()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Karamel:MaxActiveSessions", "1");
        }).CreateDefaultClient();

        var firstCreate = await client.PostAsJsonAsync("/api/sessions", new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false });
        firstCreate.EnsureSuccessStatusCode();

        var secondCreate = await client.PostAsJsonAsync("/api/sessions", new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, secondCreate.StatusCode);
        Assert.Equal("60", secondCreate.Headers.GetValues("Retry-After").Single());
        var body = await secondCreate.Content.ReadAsStringAsync();
        Assert.Contains("Server is busy", body);
    }
}

public sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "Karamel.Backend.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public record CreateResponse(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("adminToken")] string linkToken);
