using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace Karamel.Backend.Tests
{
    public class LibraryEdgeCasesTests : IClassFixture<TestServerFactory>
    {
        private readonly TestServerFactory _factory;

        public LibraryEdgeCasesTests(TestServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetLibrary_Pagination_Works()
        {
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = true, PauseBetweenSongsSeconds = 5, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            var sessionId = created!.Id;
            var token = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", token);

            // Upload 120 items
            var songs = Enumerable.Range(1, 120).Select(i => new { artist = $"Artist{i}", title = $"Title{i}", metadataJson = (string?)null }).ToArray();
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp.StatusCode);

            // Request page 1 pageSize 50 -> expect 50
            var page1 = await client.GetFromJsonAsync<SongListItem[]>($"/api/sessions/{sessionId}/library?page=1&pageSize=50");
            Assert.Equal(50, page1!.Length);

            // Request page 3 pageSize 50 -> expect 20
            var page3 = await client.GetFromJsonAsync<SongListItem[]>($"/api/sessions/{sessionId}/library?page=3&pageSize=50");
            Assert.Equal(20, page3!.Length);
        }

        [Fact]
        public async Task Post_Library_TooLarge_IsRejected()
        {
            var client = _factory.CreateDefaultClient();
            var createReq = new { RequireSingerName = true, PauseBetweenSongsSeconds = 5, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            var sessionId = created!.Id;
            var token = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", token);

            // Create >5000 items
            var songs = Enumerable.Range(1, 6000).Select(i => new { artist = $"A{i}", title = $"T{i}" }).ToArray();
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, uploadResp.StatusCode);
        }

        [Fact]
        public async Task BulkUpsert_Allows_Duplicates_WithSameArtistTitle()
        {
            var client = _factory.CreateDefaultClient();
            var createReq = new { RequireSingerName = true, PauseBetweenSongsSeconds = 5, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            var sessionId = created!.Id;
            var token = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", token);

            var songs = new[] {
                new { artist = "Dup", title = "Same" },
                new { artist = "Dup", title = "Same" },
                new { artist = "Dup", title = "Same" }
            };

            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp.StatusCode);

            var list = await client.GetFromJsonAsync<SongListItem[]>($"/api/sessions/{sessionId}/library?page=1&pageSize=50");
            Assert.Equal(3, list!.Length); // All 3 duplicates should be present
            Assert.All(list!, song => {
                Assert.Equal("Dup", song.Artist);
                Assert.Equal("Same", song.Title);
            });
            // Verify they have different IDs
            var uniqueIds = list!.Select(s => s.Id).Distinct().Count();
            Assert.Equal(3, uniqueIds);
        }

        private record CreateResponse(Guid Id, [property: JsonPropertyName("adminToken")] string linkToken);
        private record SongListItem(Guid Id, Guid SessionId, string Artist, string Title, string? MetadataJson, DateTime AddedAt);
    }
}
