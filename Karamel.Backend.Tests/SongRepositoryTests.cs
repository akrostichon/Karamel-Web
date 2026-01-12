using System;
using System.Linq;
using System.Threading.Tasks;
using Karamel.Backend.Data;
using Karamel.Backend.Repositories;
using Microsoft.EntityFrameworkCore;
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
            var repo = new EfSongRepository(ctx);
            var sessionId = Guid.NewGuid();

            var songs = new[] {
                new Controllers.SongUploadDto("Artist A", "Title 1", null),
                new Controllers.SongUploadDto("Artist A", "Title 1", null), // duplicate
                new Controllers.SongUploadDto("Artist B", "Title 2", "{\"durationMs\":12345}")
            };

            await repo.BulkUpsertAsync(sessionId, songs);

            var page = await repo.GetPageAsync(sessionId, 1, 50, null, null);
            Assert.Equal(2, page.TotalCount);
            Assert.Contains(page.Items, i => i.Artist == "Artist A" && i.Title == "Title 1");
            Assert.Contains(page.Items, i => i.Artist == "Artist B" && i.Title == "Title 2");
        }
    }
}
