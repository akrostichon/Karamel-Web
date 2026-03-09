using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
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
            var createReq = new { RequireSingerName = true, PauseBetweenSongsSeconds = 5, AllowSingersToReorder = false };
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
            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            var list = body!.Items;
            Assert.Contains(list, s => s.Artist == "A" && s.Title == "T");
        }

        [Fact]
        public async Task BulkUpsert_PreservesClientProvidedIds()
        {
            // Purpose: Verify that client-provided song IDs are preserved through the entire backend pipeline
            var client = _factory.CreateDefaultClient();

            // Create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
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
            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            var list = body!.Items;
            Assert.Equal(3, list.Length);

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
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
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
            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            var list = body!.Items;

            // Assert only 1 song exists with ID songId
            Assert.Single(list);
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
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
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
            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            var list = body!.Items;

            // Assert both songs are stored
            Assert.Equal(2, list.Length);
            Assert.Contains(list, s => s.Id == songId1 && s.Artist == "A" && s.Title == "B");
            Assert.Contains(list, s => s.Id == songId2 && s.Artist == "A" && s.Title == "B");
        }

        [Fact]
        public async Task BulkUpsert_AcceptsVideoWithValidDuration()
        {
            // Purpose: Verify videos under 15 minutes duration are accepted
            var client = _factory.CreateDefaultClient();

            // Create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            // Upload video song with 5 minutes duration (300 seconds)
            var songId = Guid.NewGuid();
            var metadataJson = "{\"mediaType\":\"video\",\"extension\":\".mp4\",\"durationSeconds\":300}";
            var songs = new[]
            {
                new { id = songId, artist = "Video Artist", title = "Short Video", metadataJson = metadataJson }
            };
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            
            // Should be accepted (202)
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp.StatusCode);

            // Verify song was persisted
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10");
            getResp.EnsureSuccessStatusCode();
            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            var list = body!.Items;
            Assert.Single(list);
            Assert.Equal(songId, list[0].Id);
        }

        [Fact]
        public async Task BulkUpsert_RejectsVideoWithExcessiveDuration()
        {
            // Purpose: Verify videos over 15 minutes duration are rejected
            var client = _factory.CreateDefaultClient();

            // Create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            // Upload video song with 20 minutes duration (1200 seconds)
            var songId = Guid.NewGuid();
            var metadataJson = "{\"mediaType\":\"video\",\"extension\":\".mp4\",\"durationSeconds\":1200}";
            var songs = new[]
            {
                new { id = songId, artist = "Video Artist", title = "Long Video", metadataJson = metadataJson }
            };
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            
            // Should be rejected (400 BadRequest)
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, uploadResp.StatusCode);

            // Verify error message mentions duration
            var errorContent = await uploadResp.Content.ReadAsStringAsync();
            Assert.Contains("duration", errorContent, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BulkUpsert_HandlesInvalidMetadataJsonGracefully()
        {
            // Purpose: Verify malformed MetadataJson doesn't crash the upload
            var client = _factory.CreateDefaultClient();

            // Create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            // Upload with invalid JSON (missing closing brace)
            var songId = Guid.NewGuid();
            var invalidJson = "{\"mediaType\":\"video\",\"durationSeconds";
            var songs = new[]
            {
                new { id = songId, artist = "Video Artist", title = "Malformed Metadata", metadataJson = invalidJson }
            };
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            
            // Should accept (treat as non-video since parsing fails)
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp.StatusCode);

            // Verify song was persisted (treated as regular song)
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10");
            getResp.EnsureSuccessStatusCode();
            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            var list = body!.Items;
            Assert.Single(list);
            Assert.Equal(songId, list[0].Id);
        }

        [Fact]
        public async Task BulkUpsert_RejectsMultipleVideosInBatch()
        {
            // Purpose: Verify rejection applies to batch uploads with mixed content
            var client = _factory.CreateDefaultClient();

            // Create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            // Upload batch: 1 valid video, 1 invalid (long) video, 1 regular song
            var validVideoId = Guid.NewGuid();
            var longVideoId = Guid.NewGuid();
            var regularSongId = Guid.NewGuid();
            
            var validVideoMetadata = "{\"mediaType\":\"video\",\"extension\":\".mp4\",\"durationSeconds\":300}";
            var longVideoMetadata = "{\"mediaType\":\"video\",\"extension\":\".mp4\",\"durationSeconds\":1800}";
            
            var songs = new[]
            {
                new { id = validVideoId, artist = "Artist1", title = "Valid Video", metadataJson = (string?)validVideoMetadata },
                new { id = longVideoId, artist = "Artist2", title = "Long Video", metadataJson = (string?)longVideoMetadata },
                new { id = regularSongId, artist = "Artist3", title = "Regular Song", metadataJson = (string?)null }
            };
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            
            // Should be rejected due to one invalid video
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, uploadResp.StatusCode);
        }

        // ── T008: Fuzzy search integration tests ─────────────────────────────

        [Fact]
        public async Task GetPage_WithTypoQuery_ReturnsFuzzyMatchedSong()
        {
            // Purpose: "Bohemian Rapsody" (1-char typo) should return "Bohemian Rhapsody"
            var client = _factory.CreateDefaultClient();

            // Create session and upload songs
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            var songs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Queen", title = "Bohemian Rhapsody", metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "The Beatles", title = "Hey Jude", metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "Led Zeppelin", title = "Stairway to Heaven", metadataJson = (string?)null },
            };
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp.StatusCode);

            // Query with a typo: "Rapsody" (missing 'h')
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10&search=Rapsody");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);

            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            Assert.NotEmpty(body!.Items);
            Assert.Contains(body.Items, s => s.Title == "Bohemian Rhapsody");
        }

        [Fact]
        public async Task GetPage_ReturnsLibraryResponseDtoShape_NotPlainArray()
        {
            // Purpose: Response body must be a JSON object with 'items', 'totalCount', 'page', 'pageSize', 'suggestions'
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            var uploadSongs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Test Artist", title = "Test Title", metadataJson = (string?)null },
            };
            await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", uploadSongs);

            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);

            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            Assert.NotNull(body!.Items);
            Assert.True(body.TotalCount >= 1);
            Assert.Equal(1, body.Page);
            Assert.NotNull(body.Suggestions);
        }

        [Fact]
        public async Task GetPage_ExactQuery_ReturnsSuggestionsEmpty()
        {
            // Purpose: When results are found (no zero-results), suggestions must be empty
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            var linkToken = created.linkToken;
            client.DefaultRequestHeaders.Add("X-Link-Token", linkToken);

            var uploadSongs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Queen", title = "Bohemian Rhapsody", metadataJson = (string?)null },
            };
            await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", uploadSongs);

            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10&search=Bohemian");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);

            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            Assert.NotEmpty(body!.Items);
            Assert.Empty(body.Suggestions);
        }

        // ── T017: Relevance ordering assertions ──────────────────────────────────

        [Fact]
        public async Task GetPage_RelevanceOrdering_ExactBeforePartialBeforeArtistOnlyBeforeFuzzy()
        {
            // Purpose: Results must be sorted by tier: ExactTitle(0) < PartialTitle(1) < ArtistOnly(2) < FuzzyMatch(3)
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            client.DefaultRequestHeaders.Add("X-Link-Token", created.linkToken);

            // Seeds one song per tier for query "Yesterday" (length 9 → threshold 2)
            var songs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Z Artist",       title = "Yesterday",        metadataJson = (string?)null }, // ExactTitle
                new { id = Guid.NewGuid(), artist = "Artist B",        title = "Yesterday Mix",    metadataJson = (string?)null }, // PartialTitle
                new { id = Guid.NewGuid(), artist = "Yesterday Echo",  title = "Some Other Song",  metadataJson = (string?)null }, // ArtistOnly
                new { id = Guid.NewGuid(), artist = "Artist D",        title = "Yesterdai",        metadataJson = (string?)null }, // FuzzyMatch (OSA dist=1)
                new { id = Guid.NewGuid(), artist = "Unrelated Band",  title = "Stairway to Heaven", metadataJson = (string?)null }, // no match
            };
            await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);

            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=20&search=Yesterday");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);
            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            var items = body!.Items;

            var exactIdx    = Array.FindIndex(items, s => s.Title == "Yesterday" && s.Artist == "Z Artist");
            var partialIdx  = Array.FindIndex(items, s => s.Title == "Yesterday Mix");
            var artistIdx   = Array.FindIndex(items, s => s.Artist == "Yesterday Echo");
            var fuzzyIdx    = Array.FindIndex(items, s => s.Title == "Yesterdai");
            var unrelatedIdx = Array.FindIndex(items, s => s.Title == "Stairway to Heaven");

            Assert.True(exactIdx >= 0,   "ExactTitle song must appear in results");
            Assert.True(partialIdx >= 0, "PartialTitle song must appear in results");
            Assert.True(artistIdx >= 0,  "ArtistOnly song must appear in results");
            Assert.True(fuzzyIdx >= 0,   "FuzzyMatch song must appear in results");
            Assert.Equal(-1, unrelatedIdx); // unrelated must be filtered out

            Assert.True(exactIdx < partialIdx,  "ExactTitle must rank before PartialTitle");
            Assert.True(partialIdx < artistIdx, "PartialTitle must rank before ArtistOnly");
            Assert.True(artistIdx < fuzzyIdx,   "ArtistOnly must rank before FuzzyMatch");
        }

        [Fact]
        public async Task GetPage_AlphabeticalOrderingWithinTier()
        {
            // Purpose: Within the same tier (PartialTitle here), results must be sorted by Artist then Title
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            client.DefaultRequestHeaders.Add("X-Link-Token", created.linkToken);

            // All songs have title containing "test" → all land in PartialTitle tier
            // Uploaded in reverse alphabetical order to confirm sorting is applied
            var songs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Artist C", title = "Testing Something", metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "Artist A", title = "Testing Now",       metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "Artist A", title = "Testing Always",    metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "Artist B", title = "Testing Again",     metadataJson = (string?)null },
            };
            await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);

            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10&search=test");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);
            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);
            var items = body!.Items;

            Assert.Equal(4, items.Length);
            // Expected: Artist A/Always, Artist A/Now, Artist B/Again, Artist C/Something
            Assert.Equal("Artist A",       items[0].Artist);
            Assert.Equal("Testing Always", items[0].Title);
            Assert.Equal("Artist A",       items[1].Artist);
            Assert.Equal("Testing Now",    items[1].Title);
            Assert.Equal("Artist B",       items[2].Artist);
            Assert.Equal("Artist C",       items[3].Artist);
        }

        // ── T018: Cross-page relevance ordering ──────────────────────────────

        [Fact]
        public async Task GetPage_CrossPageOrdering_TierIsMonotoneNonDecreasing()
        {
            // Purpose: Tier sequence must be non-decreasing across pagination boundaries.
            // Higher-priority tiers appear on earlier pages; lower-priority tiers on later pages.
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            client.DefaultRequestHeaders.Add("X-Link-Token", created.linkToken);

            // Query "Yesterday" (length 9 → threshold 2). Tiers:
            //   ExactTitle:    "Yesterday"              by "Z Artist"
            //   PartialTitle:  "Yesterday Album/Mix/Remix" by A/B/C Artist
            //   ArtistOnly:    Artist="Yesterday Club/Duo"
            //   FuzzyMatch:    "Yesterdai"              by "D Artist"  (OSA dist=1)
            var songs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Z Artist",       title = "Yesterday",          metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "A Artist",        title = "Yesterday Album",    metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "B Artist",        title = "Yesterday Mix",      metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "C Artist",        title = "Yesterday Remix",    metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "Yesterday Club",  title = "Alpha Song",         metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "Yesterday Duo",   title = "Beta Song",          metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "D Artist",        title = "Yesterdai",          metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "Unrelated Band",  title = "Stairway to Heaven", metadataJson = (string?)null },
            };
            await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);

            var page1Resp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=3&search=Yesterday");
            var page2Resp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=2&pageSize=3&search=Yesterday");
            var page3Resp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=3&pageSize=3&search=Yesterday");

            Assert.Equal(System.Net.HttpStatusCode.OK, page1Resp.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.OK, page2Resp.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.OK, page3Resp.StatusCode);

            var page1 = (await page1Resp.Content.ReadFromJsonAsync<LibraryResponseBody>())!;
            var page2 = (await page2Resp.Content.ReadFromJsonAsync<LibraryResponseBody>())!;
            var page3 = (await page3Resp.Content.ReadFromJsonAsync<LibraryResponseBody>())!;

            // 7 matching songs (Stairway excluded)
            Assert.Equal(7, page1.TotalCount);

            // ExactTitle song must lead page 1
            Assert.Contains(page1.Items, s => s.Title == "Yesterday" && s.Artist == "Z Artist");

            // ArtistOnly songs must not appear on page 1 (lower priority)
            Assert.DoesNotContain(page1.Items, s => s.Artist == "Yesterday Club");
            Assert.DoesNotContain(page1.Items, s => s.Artist == "Yesterday Duo");

            // FuzzyMatch song must not appear on page 1 or page 2
            Assert.DoesNotContain(page1.Items, s => s.Title == "Yesterdai");
            Assert.DoesNotContain(page2.Items, s => s.Title == "Yesterdai");

            // FuzzyMatch song must appear on page 3
            Assert.Contains(page3.Items, s => s.Title == "Yesterdai");

            // ExactTitle song must not appear on page 2 or 3
            Assert.DoesNotContain(page2.Items, s => s.Title == "Yesterday" && s.Artist == "Z Artist");
            Assert.DoesNotContain(page3.Items, s => s.Title == "Yesterday" && s.Artist == "Z Artist");
        }

        // ── T020: Zero-results suggestions tests (US3) ───────────────────────

        [Fact]
        public async Task GetPage_WhenQueryHasZeroResults_ReturnsSuggestionsFromLibrary()
        {
            // Purpose: A query that matches nothing via substring or fuzzy (score>threshold) must
            // return items:[] and suggestions:[ ...close token matches from seeded library ].
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            client.DefaultRequestHeaders.Add("X-Link-Token", created.linkToken);

            // Seed a library with "Beyonce" as artist
            var songs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Beyonce", title = "Crazy In Love", metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "Beatles", title = "Hey Jude",      metadataJson = (string?)null },
            };
            await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);

            // "Beyonsay" (8 chars, threshold=2) — OSA dist from "beyonce" ≈ 3, so no FuzzyMatch
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10&search=Beyonsay");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);

            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);

            // No songs should match
            Assert.Empty(body!.Items);

            // But spelling suggestions should be populated
            Assert.NotEmpty(body.Suggestions);
        }

        [Fact]
        public async Task GetPage_WhenFuzzyMatchFound_ReturnsSuggestionsEmpty()
        {
            // Purpose: When results ARE returned (even via fuzzy), suggestions must be empty.
            var client = _factory.CreateDefaultClient();

            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            client.DefaultRequestHeaders.Add("X-Link-Token", created.linkToken);

            var songs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Queen", title = "Bohemian Rhapsody", metadataJson = (string?)null },
            };
            await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);

            // "Rapsody" (7 chars, threshold=1) — fuzzy match on "Rhapsody" title token (OSA dist=1)
            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library?page=1&pageSize=10&search=Rapsody");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);

            var body = await getResp.Content.ReadFromJsonAsync<LibraryResponseBody>();
            Assert.NotNull(body);

            Assert.NotEmpty(body!.Items);  // Fuzzy match found
            Assert.Empty(body.Suggestions); // No suggestions needed when results present
        }

        // ── T025: Round-trip serialization tests ─────────────────────────────

        [Fact]
        public void SearchSuggestionDto_RoundTripSerialization_PreservesAllProperties()
        {
            // Arrange
            var original = new Karamel.Backend.Controllers.SearchSuggestionDto("beyonce", "artist");

            // Act — serialize then deserialize
            var json = System.Text.Json.JsonSerializer.Serialize(original);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<Karamel.Backend.Controllers.SearchSuggestionDto>(json);

            // Assert — camelCase JSON property names
            Assert.Contains("\"text\"", json);
            Assert.Contains("\"sourceField\"", json);
            Assert.NotNull(deserialized);
            Assert.Equal(original.Text, deserialized!.Text);
            Assert.Equal(original.SourceField, deserialized.SourceField);
        }

        [Fact]
        public void LibraryResponseDto_RoundTripSerialization_PreservesAllProperties()
        {
            // Arrange
            var songId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var addedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var item = new Karamel.Backend.Controllers.SongListItemDto(songId, sessionId, "Queen", "Bohemian Rhapsody", null, addedAt);
            var suggestion = new Karamel.Backend.Controllers.SearchSuggestionDto("rhapsody", "title");
            var original = new Karamel.Backend.Controllers.LibraryResponseDto(
                Items: new[] { item },
                TotalCount: 1,
                Page: 1,
                PageSize: 10,
                Suggestions: new[] { suggestion }
            );

            // Act — serialize then deserialize
            var json = System.Text.Json.JsonSerializer.Serialize(original);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<Karamel.Backend.Controllers.LibraryResponseDto>(json);

            // Assert — camelCase JSON property names
            Assert.Contains("\"items\"", json);
            Assert.Contains("\"totalCount\"", json);
            Assert.Contains("\"page\"", json);
            Assert.Contains("\"pageSize\"", json);
            Assert.Contains("\"suggestions\"", json);
            Assert.NotNull(deserialized);
            Assert.Equal(original.TotalCount, deserialized!.TotalCount);
            Assert.Equal(original.Page, deserialized.Page);
            Assert.Equal(original.PageSize, deserialized.PageSize);
            Assert.Single(deserialized.Items);
            Assert.Single(deserialized.Suggestions);
            Assert.Equal("rhapsody", deserialized.Suggestions.First().Text);
            Assert.Equal("title", deserialized.Suggestions.First().SourceField);
        }

        private record CreateResponse(Guid Id, [property: JsonPropertyName("adminToken")] string linkToken);
        private record SongListItem(Guid Id, Guid SessionId, string Artist, string Title, string? MetadataJson, DateTime AddedAt);

        // DTO for reading the new LibraryResponseDto shape in tests
        private record LibraryResponseBody(
            [property: JsonPropertyName("items")] SongListItem[] Items,
            [property: JsonPropertyName("totalCount")] long TotalCount,
            [property: JsonPropertyName("page")] int Page,
            [property: JsonPropertyName("pageSize")] int PageSize,
            [property: JsonPropertyName("suggestions")] SuggestionItem[] Suggestions
        );
        private record SuggestionItem(
            [property: JsonPropertyName("text")] string Text,
            [property: JsonPropertyName("sourceField")] string SourceField
        );

        // ── T006: GetArtists endpoint tests ─────────────────────────────────

        private record ArtistSummaryResponse(
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("songCount")] int SongCount
        );

        [Fact]
        public async Task GetArtists_WithSeededSession_ReturnsSortedArtistArray()
        {
            var client = _factory.CreateDefaultClient();

            var sessionResp = await client.PostAsJsonAsync("/api/sessions",
                new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false });
            sessionResp.EnsureSuccessStatusCode();
            var created = await sessionResp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;
            client.DefaultRequestHeaders.Add("X-Link-Token", created.linkToken);

            var songs = new[]
            {
                new { id = Guid.NewGuid(), artist = "Adele",  title = "Hello",           metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "ABBA",   title = "Dancing Queen",   metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "ABBA",   title = "Waterloo",        metadataJson = (string?)null },
                new { id = Guid.NewGuid(), artist = "AC/DC",  title = "Back in Black",   metadataJson = (string?)null },
            };
            var uploadResp = await client.PostAsJsonAsync($"/api/sessions/{sessionId}/library/bulk", songs);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, uploadResp.StatusCode);

            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library/artists");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);

            var artists = await getResp.Content.ReadFromJsonAsync<ArtistSummaryResponse[]>();
            Assert.NotNull(artists);
            Assert.Equal(3, artists!.Length);

            // Case-insensitive alphabetical order: ABBA, AC/DC, Adele
            Assert.Equal("ABBA",  artists[0].Name);
            Assert.Equal(2,       artists[0].SongCount);
            Assert.Equal("AC/DC", artists[1].Name);
            Assert.Equal(1,       artists[1].SongCount);
            Assert.Equal("Adele", artists[2].Name);
            Assert.Equal(1,       artists[2].SongCount);

            // Verify JSON field names
            var raw = await (await client.GetAsync($"/api/sessions/{sessionId}/library/artists")).Content.ReadAsStringAsync();
            Assert.Contains("\"name\"",      raw);
            Assert.Contains("\"songCount\"", raw);
        }

        [Fact]
        public async Task GetArtists_WithNoSongs_ReturnsEmptyArray()
        {
            var client = _factory.CreateDefaultClient();

            var sessionResp = await client.PostAsJsonAsync("/api/sessions",
                new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false });
            sessionResp.EnsureSuccessStatusCode();
            var created = await sessionResp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            var sessionId = created!.Id;

            var getResp = await client.GetAsync($"/api/sessions/{sessionId}/library/artists");
            Assert.Equal(System.Net.HttpStatusCode.OK, getResp.StatusCode);

            var artists = await getResp.Content.ReadFromJsonAsync<ArtistSummaryResponse[]>();
            Assert.NotNull(artists);
            Assert.Empty(artists!);
        }
    }
}
