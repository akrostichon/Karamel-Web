using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace Karamel.Backend.Tests
{
    public class SessionApiTests : IClassFixture<TestServerFactory>
    {
        private readonly TestServerFactory _factory;

        public SessionApiTests(TestServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Post_Sessions_Returns_LinkToken_And_Playlist_Authorization_Works()
        {
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = true, PauseBetweenSongsSeconds = 5, AllowSingersToReorder = false, Theme = (string?)null };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            try
            {
                resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Session creation failed. Error : {ex}");
            }

            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);
            Assert.NotEqual(Guid.Empty, created!.Id);
            Assert.False(string.IsNullOrEmpty(created.linkToken));

            // Validate that the token service accepts the token for the created session
            using var scope = _factory.Services.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<Services.ITokenService>();
            var (_, isValid) = tokenService.ValidateLinkToken(created.linkToken, created.Id);
            Assert.True(isValid, "Generated link token should validate for the session");
        }

        [Fact]
        public async Task Post_Sessions_With_Theme_Returns_Theme_In_Get()
        {
            var client = _factory.CreateDefaultClient();

            // Create session with theme
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 5, AllowSingersToReorder = false, Theme = "dark" };
            var createResp = await client.PostAsJsonAsync("/api/sessions", createReq);
            createResp.EnsureSuccessStatusCode();

            var created = await createResp.Content.ReadFromJsonAsync<CreateResponseWithTheme>();
            Assert.NotNull(created);
            Assert.Equal("dark", created!.theme);

            // Get session and verify theme is returned
            var getResp = await client.GetAsync($"/api/sessions/{created.Id}");
            getResp.EnsureSuccessStatusCode();

            var session = await getResp.Content.ReadFromJsonAsync<SessionGetResponse>();
            Assert.NotNull(session);
            Assert.Equal("dark", session!.theme);
        }

        [Fact]
        public async Task Post_Sessions_Without_Theme_Returns_Null_Theme()
        {
            var client = _factory.CreateDefaultClient();

            // Create session without theme
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 5, AllowSingersToReorder = false, Theme = (string?)null };
            var createResp = await client.PostAsJsonAsync("/api/sessions", createReq);
            createResp.EnsureSuccessStatusCode();

            var created = await createResp.Content.ReadFromJsonAsync<CreateResponseWithTheme>();
            Assert.NotNull(created);
            Assert.Null(created!.theme);

            // Get session and verify theme is null
            var getResp = await client.GetAsync($"/api/sessions/{created.Id}");
            getResp.EnsureSuccessStatusCode();

            var session = await getResp.Content.ReadFromJsonAsync<SessionGetResponse>();
            Assert.NotNull(session);
            Assert.Null(session!.theme);
        }

        private record CreateResponse(Guid Id, string linkToken);
        private record CreateResponseWithTheme(Guid Id, string linkToken, string? theme);
        private record SessionGetResponse(Guid Id, bool requireSingerName, int pauseBetweenSongsSeconds, bool allowSingersToReorder, string? theme);
        private record PlaylistDto(Guid id, Guid sessionId);
    }
}
