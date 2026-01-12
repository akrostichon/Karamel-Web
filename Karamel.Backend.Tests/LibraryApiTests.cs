using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace Karamel.Backend.Tests
{
    public class LibraryApiTests : IClassFixture<TestServerFactory>
    {
        private readonly TestServerFactory _factory;

        public LibraryApiTests(TestServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Post_Library_Bulk_Requires_LinkToken_And_Persists()
        {
            var client = _factory.CreateDefaultClient();

            // Create a session first
            var createReq = new { RequireSingerName = true, PauseBetweenSongsSeconds = 5 };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;

            // Without token should be unauthorized
            var songs = new[] { new { artist = "A", title = "T" } };
            var noAuthResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, noAuthResp.StatusCode);

            // With token set in header should be accepted
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);
            var okResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, okResp.StatusCode);

            // Now GET paginated results
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10");
            getResp.EnsureSuccessStatusCode();
            var list = await getResp.Content.ReadFromJsonAsync<SongListItem[]>();
            Assert.NotNull(list);
            Assert.Contains(list, s => s.Artist == "A" && s.Title == "T");
        }

        private record CreateResponse(Guid Id, string linkToken);
        private record SongListItem(Guid Id, Guid SessionId, string Artist, string Title, string? MetadataJson, DateTime AddedAt);
    }
}
