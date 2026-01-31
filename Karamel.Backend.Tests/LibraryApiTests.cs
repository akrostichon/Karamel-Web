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

        [Fact]
        public async Task BulkUpsert_PreservesClientProvidedIds()
        {
            // Purpose: Verify that client-provided song IDs are preserved through the entire backend pipeline
            var client = _factory.CreateDefaultClient();

            // Create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1 };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            // Upload 3 songs with specific GUIDs
            var songId1 = Guid.NewGuid();
            var songId2 = Guid.NewGuid();
            var songId3 = Guid.NewGuid();
            var songs = new[]
            {
                new { id = songId1, artist = "Artist1", title = "Title1", metadataJson = (string?)null },
                new { id = songId2, artist = "Artist2", title = "Title2", metadataJson = (string?)null },
                new { id = songId3, artist = "Artist3", title = "Title3", metadataJson = (string?)null }
            };
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp.StatusCode);

            // Query songs from backend via GET endpoint
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10");
            getResp.EnsureSuccessStatusCode();
            var list = await getResp.Content.ReadFromJsonAsync<SongListItem[]>();
            Assert.NotNull(list);
            Assert.Equal(3, list!.Length);

            // Assert that returned song IDs match the uploaded IDs exactly
            Assert.Contains(list, s => s.Id == songId1 && s.Artist == "Artist1" && s.Title == "Title1");
            Assert.Contains(list, s => s.Id == songId2 && s.Artist == "Artist2" && s.Title == "Title2");
            Assert.Contains(list, s => s.Id == songId3 && s.Artist == "Artist3" && s.Title == "Title3");
        }

        [Fact]
        public async Task BulkUpsert_DeduplicatesByIdNotArtistTitle()
        {
            // Purpose: Confirm ID-based deduplication works correctly (replaced old Artist+Title deduplication)
            var client = _factory.CreateDefaultClient();

            // Create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1 };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            // Upload song with ID abc123, Artist "X", Title "Y"
            var songId = Guid.NewGuid();
            var songs1 = new[]
            {
                new { id = songId, artist = "X", title = "Y", metadataJson = (string?)null }
            };
            var uploadResp1 = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs1);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp1.StatusCode);

            // Upload same ID with different Artist "Z", Title "W"
            var songs2 = new[]
            {
                new { id = songId, artist = "Z", title = "W", metadataJson = (string?)null }
            };
            var uploadResp2 = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs2);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp2.StatusCode);

            // Query songs
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10");
            getResp.EnsureSuccessStatusCode();
            var list = await getResp.Content.ReadFromJsonAsync<SongListItem[]>();
            Assert.NotNull(list);

            // Assert only 1 song exists with ID songId
            Assert.Single(list!);
            Assert.Equal(songId, list[0].Id);
            // Assert Artist/Title reflects the latest upload
            Assert.Equal("Z", list[0].Artist);
            Assert.Equal("W", list[0].Title);
        }

        [Fact]
        public async Task BulkUpsert_AllowsDuplicateArtistTitleWithDifferentIds()
        {
            // Purpose: Prove that Artist+Title can now be duplicated (e.g., different file versions of same song)
            var client = _factory.CreateDefaultClient();

            // Create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1 };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            // Upload 2 songs with same Artist "A", Title "B" but different IDs
            var songId1 = Guid.NewGuid();
            var songId2 = Guid.NewGuid();
            var songs = new[]
            {
                new { id = songId1, artist = "A", title = "B", metadataJson = (string?)null },
                new { id = songId2, artist = "A", title = "B", metadataJson = (string?)null }
            };
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp.StatusCode);

            // Query songs
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10");
            getResp.EnsureSuccessStatusCode();
            var list = await getResp.Content.ReadFromJsonAsync<SongListItem[]>();
            Assert.NotNull(list);

            // Assert both songs are stored
            Assert.Equal(2, list!.Length);
            Assert.Contains(list, s => s.Id == songId1 && s.Artist == "A" && s.Title == "B");
            Assert.Contains(list, s => s.Id == songId2 && s.Artist == "A" && s.Title == "B");
        }

        private record CreateResponse(Guid Id, string linkToken);
        private record SongListItem(Guid Id, Guid SessionId, string Artist, string Title, string? MetadataJson, DateTime AddedAt);
    }
}
