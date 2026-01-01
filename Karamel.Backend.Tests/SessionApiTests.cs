using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

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

            // create a playlist for this session
            var createPlaylistResp = await client.PostAsync($"/api/playlists/{created.Id}", null);
            createPlaylistResp.EnsureSuccessStatusCode();
            var playlist = await createPlaylistResp.Content.ReadFromJsonAsync<PlaylistDto>();

            // Try to add item without token - should be Unauthorized
            var addItem = new { Artist = "A", Title = "B", SingerName = "S" };
            var addRespNoToken = await client.PostAsJsonAsync($"/api/playlists/{created.Id}/{playlist!.id}/items", addItem);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, addRespNoToken.StatusCode);

            // Add with token in header
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/playlists/{created.Id}/{playlist!.id}/items");
            request.Headers.Add("X-Link-Token", created.linkToken);
            request.Content = JsonContent.Create(addItem);
            var addResp = await client.SendAsync(request);
            addResp.EnsureSuccessStatusCode();
        }

        private record CreateResponse(Guid Id, string linkToken);
        private record PlaylistDto(Guid id, Guid sessionId);
    }
}
