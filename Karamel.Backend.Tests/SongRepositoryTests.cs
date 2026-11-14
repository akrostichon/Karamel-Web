using System;
using System.Linq;
using System.Threading.Tasks;
using Karamel.Backend.Data;
using Karamel.Backend.Repositories;
using Karamel.Backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Karamel.Backend.Tests
{
    public class SongRepositoryTests
    {
        private BackendDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BackendDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new BackendDbContext(options);
        }

        [Fact]
        public async Task BulkUpsert_AddsDistinctSongs()
        {
            using var ctx = CreateContext();
            var repo = new EfSongRepository(ctx, NullLogger<EfSongRepository>.Instance, new FuzzySearchService());
            var sessionId = Guid.NewGuid();

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            // Test: Different IDs with same artist/title are allowed (duplicates by content, but unique IDs)
            var songs = new[] {
                new Controllers.SongUploadDto(id1, "Artist A", "Title 1", null),
                new Controllers.SongUploadDto(id2, "Artist A", "Title 1", null), // Same artist/title but different ID - allowed
                new Controllers.SongUploadDto(id3, "Artist B", "Title 2", "{\"durationMs\":12345}")
            };

            await repo.BulkUpsertAsync(sessionId, songs);

            var page = await repo.GetPageAsync(sessionId, 1, 50, null, null);
            Assert.Equal(3, page.TotalCount); // All 3 should be added (different IDs)
            Assert.Equal(2, page.Items.Count(i => i.Artist == "Artist A" && i.Title == "Title 1")); // Two songs with same content
            Assert.Contains(page.Items, i => i.Artist == "Artist B" && i.Title == "Title 2");
        }

        [Fact]
        public async Task BulkUpsert_WithSameId_UpdatesExistingSong()
        {
            using var ctx = CreateContext();
            var repo = new EfSongRepository(ctx, NullLogger<EfSongRepository>.Instance, new FuzzySearchService());
            var sessionId = Guid.NewGuid();

            var songId = Guid.NewGuid();

            // First insert
            var firstBatch = new[] {
                new Controllers.SongUploadDto(songId, "Artist Original", "Title Original", null)
            };
            await repo.BulkUpsertAsync(sessionId, firstBatch);

            // Verify initial insert
            var page1 = await repo.GetPageAsync(sessionId, 1, 50, null, null);
            Assert.Equal(1, page1.TotalCount);
            var original = page1.Items.First();
            Assert.Equal("Artist Original", original.Artist);
            Assert.Equal("Title Original", original.Title);

            // Second upsert with same ID but different data
            var secondBatch = new[] {
                new Controllers.SongUploadDto(songId, "Artist Updated", "Title Updated", "{\"updated\":true}")
            };
            await repo.BulkUpsertAsync(sessionId, secondBatch);

            // Verify update (still only 1 song, but with updated data)
            var page2 = await repo.GetPageAsync(sessionId, 1, 50, null, null);
            Assert.Equal(1, page2.TotalCount);
            var updated = page2.Items.First();
            Assert.Equal(songId, updated.Id);
            Assert.Equal("Artist Updated", updated.Artist);
            Assert.Equal("Title Updated", updated.Title);
            Assert.Contains("\"updated\":true", updated.MetadataJson);
        }

        [Fact]
        public async Task GetPage_WithSqliteSearch_IgnoresDiacriticsAndSortsByFoldedText()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<BackendDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var ctx = new BackendDbContext(options);
            await ctx.Database.EnsureCreatedAsync();

            var repo = new EfSongRepository(ctx, NullLogger<EfSongRepository>.Instance, new FuzzySearchService());
            var sessionId = Guid.NewGuid();
            var dieArzteId = Guid.NewGuid();
            var arzteId = Guid.NewGuid();

            await repo.BulkUpsertAsync(sessionId, new[]
            {
                new Controllers.SongUploadDto(dieArzteId, "Die Ärzte", "Schrei nach Liebe", null),
                new Controllers.SongUploadDto(Guid.NewGuid(), "ABBA", "Waterloo", null),
                new Controllers.SongUploadDto(Guid.NewGuid(), "Zappa", "Dinah-Moe Humm", null),
                new Controllers.SongUploadDto(Guid.NewGuid(), "Falco", "Jeanny", null),
                new Controllers.SongUploadDto(Guid.NewGuid(), "Helene Fischer", "Atemlos durch die Nacht", null),
                new Controllers.SongUploadDto(Guid.NewGuid(), "Straße der Liebe", "Lied", null),
                new Controllers.SongUploadDto(arzteId, "Ärzte", "Unreleased", null),
                new Controllers.SongUploadDto(Guid.NewGuid(), "Boney M", "Rivers of Babylon", null)
            });

            Guid[]? firstResultIds = null;
            foreach (var search in new[] { "ärzte", "Ärzte", "arzte", "ARZTE" })
            {
                var page = await repo.GetPageAsync(sessionId, 1, 50, search, null);
                var resultIds = page.Items.Select(item => item.Id).OrderBy(id => id).ToArray();

                Assert.NotEmpty(resultIds);
                Assert.Equal(new[] { dieArzteId, arzteId }.OrderBy(id => id), resultIds);
                firstResultIds ??= resultIds;
                Assert.Equal(firstResultIds, resultIds);
            }

            var strassePage = await repo.GetPageAsync(sessionId, 1, 50, "strasse", null);
            Assert.Contains(strassePage.Items, item => item.Artist == "Straße der Liebe");

            var sortedPage = await repo.GetPageAsync(sessionId, 1, 50, null, null);
            var arzteIndex = sortedPage.Items.ToList().FindIndex(item => item.Artist == "Ärzte");
            var boneyIndex = sortedPage.Items.ToList().FindIndex(item => item.Artist == "Boney M");
            var dieArzteIndex = sortedPage.Items.ToList().FindIndex(item => item.Artist == "Die Ärzte");
            var zappaIndex = sortedPage.Items.ToList().FindIndex(item => item.Artist == "Zappa");

            Assert.True(arzteIndex >= 0 && arzteIndex < boneyIndex);
            Assert.True(dieArzteIndex >= 0 && dieArzteIndex < zappaIndex);
        }
    }
}
