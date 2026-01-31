using System;
using System.Linq;
using System.Threading.Tasks;
using Karamel.Backend.Data;
using Karamel.Backend.Repositories;
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
            var repo = new EfSongRepository(ctx, NullLogger<EfSongRepository>.Instance);
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
            var repo = new EfSongRepository(ctx, NullLogger<EfSongRepository>.Instance);
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
    }
}
