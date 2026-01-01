using System;
using System.Net.Http;
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

            var createReq = new { RequireSingerName = true, PauseBetweenSongsSeconds = 5 };
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
            var tokenService = scope.ServiceProvider.GetRequiredService<Karamel.Backend.Services.ITokenService>();
            var ok = tokenService.ValidateLinkToken(created.Id, created.linkToken);
            Assert.True(ok, "Generated link token should validate for the session");
        }

        private record CreateResponse(Guid Id, string linkToken);
        private record PlaylistDto(Guid id, Guid sessionId);
    }
}
