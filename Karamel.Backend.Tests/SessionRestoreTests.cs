using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Karamel.Backend.Models;
using Karamel.Backend.Repositories;
using Karamel.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Karamel.Backend.Tests;

public class SessionRestoreTests : IClassFixture<TestServerFactory>
{
    private readonly TestServerFactory _factory;

    public SessionRestoreTests(TestServerFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Restore_AfterSessionWasDeleted_RecreatesSessionAndKeepsTokensValid()
    {
        var client = _factory.CreateDefaultClient();
        var created = await CreateSessionAsync(client);

        var endRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{created.Id}/end")
        {
            Content = JsonContent.Create(new { Force = true })
        };
        endRequest.Headers.Add("X-Link-Token", created.AdminToken);
        var endResponse = await client.SendAsync(endRequest);
        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);

        var restoreResponse = await RestoreAsync(client, created.Id, created.AdminToken);
        Assert.Equal(HttpStatusCode.Created, restoreResponse.StatusCode);
        var restored = await restoreResponse.Content.ReadFromJsonAsync<RestoreResponse>();
        Assert.NotNull(restored);
        Assert.True(restored!.Restored);
        Assert.Equal(created.AdminToken, restored.AdminToken);
        Assert.Equal(created.SingerToken, restored.SingerToken);

        using (var scope = _factory.Services.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var (role, valid) = tokenService.ValidateToken(created.SingerToken, created.Id);
            Assert.True(valid);
            Assert.Equal("singer", role);
        }

        var getResponse = await client.GetAsync($"/api/sessions/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{created.Id}/library/bulk")
        {
            Content = JsonContent.Create(new[]
            {
                new { id = Guid.NewGuid(), artist = "Restored", title = "Session", metadataJson = (string?)null }
            })
        };
        uploadRequest.Headers.Add("X-Link-Token", created.AdminToken);
        var uploadResponse = await client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Restore_ExistingSession_ExtendsExpiryAndReturnsRestoredFalse()
    {
        var client = _factory.CreateDefaultClient();
        var created = await CreateSessionAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var session = await repository.GetByIdAsync(created.Id);
            Assert.NotNull(session);
            session!.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            await repository.UpdateAsync(session);
        }

        var response = await RestoreAsync(client, created.Id, created.AdminToken, theme: "light");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var restored = await response.Content.ReadFromJsonAsync<RestoreResponse>();
        Assert.NotNull(restored);
        Assert.False(restored!.Restored);
        Assert.True(restored.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(created.Theme, restored.Theme);
    }

    [Fact]
    public async Task Restore_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateDefaultClient();
        var response = await RestoreAsync(client, Guid.NewGuid(), token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Restore_WithSingerToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateDefaultClient();
        var created = await CreateSessionAsync(client);

        var response = await RestoreAsync(client, created.Id, created.SingerToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Restore_WithAnotherSessionsAdminToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateDefaultClient();
        var target = await CreateSessionAsync(client);
        var other = await CreateSessionAsync(client);

        var response = await RestoreAsync(client, target.Id, other.AdminToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Restore_WhenSessionCapIsReached_ReturnsServiceUnavailable()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Karamel:MaxActiveSessions", "0");
        }).CreateDefaultClient();
        var sessionId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var adminToken = tokenService.GenerateAdminToken(sessionId);

        var response = await RestoreAsync(client, sessionId, adminToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("60", string.Join(",", response.Headers.GetValues("Retry-After")));
    }

    private static async Task<CreatedSession> CreateSessionAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/sessions", new
        {
            RequireSingerName = true,
            PauseBetweenSongsSeconds = 5,
            AllowSingersToReorder = false,
            Theme = "dark"
        });
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreatedSession>();
        Assert.NotNull(created);
        return created!;
    }

    private static async Task<HttpResponseMessage> RestoreAsync(HttpClient client, Guid sessionId, string? token, string? theme = "dark")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{sessionId}/restore")
        {
            Content = JsonContent.Create(new
            {
                RequireSingerName = false,
                PauseBetweenSongsSeconds = 1,
                AllowSingersToReorder = true,
                Theme = theme
            })
        };

        if (token is not null)
        {
            request.Headers.Add("X-Link-Token", token);
        }

        return await client.SendAsync(request);
    }

    private sealed record CreatedSession(
        Guid Id,
        [property: JsonPropertyName("adminToken")] string AdminToken,
        [property: JsonPropertyName("singerToken")] string SingerToken,
        [property: JsonPropertyName("theme")] string? Theme);

    private sealed record RestoreResponse(
        Guid Id,
        [property: JsonPropertyName("adminToken")] string AdminToken,
        [property: JsonPropertyName("singerToken")] string SingerToken,
        [property: JsonPropertyName("expiresAt")] DateTime ExpiresAt,
        [property: JsonPropertyName("theme")] string? Theme,
        [property: JsonPropertyName("restored")] bool Restored);
}
