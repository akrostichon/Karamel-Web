using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Karamel.Backend.Services;
using Karamel.Backend.Repositories;
using Karamel.Backend.Models;
using Karamel.Backend.Controllers;
using static Karamel.Backend.Models.SessionConstants;

namespace Karamel.Backend.Tests
{
    public class SessionCleanupTests : IClassFixture<TestServerFactory>
    {
        private readonly TestServerFactory _factory;

        public SessionCleanupTests(TestServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CleanupOnceAsync_DeletesExpiredSessions_AndNotifiesHub()
        {
            using var scope = _factory.Services.CreateScope();
            var services = scope.ServiceProvider;

            var repo = services.GetRequiredService<ISessionRepository>();
            var cleanup = services.GetRequiredService<SessionCleanupService>();

            // Create a session that is already expired
            var s = new Session
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
                Config = new SessionConfig
                {
                    RequireSingerName = true,
                    PauseBetweenSongsSeconds = 5,
                    AllowSingersToReorder = false
                },
                AdminToken = "test-admin-token",
                SingerToken = "test-singer-token"
            };

            await repo.AddAsync(s);

            // Ensure the stored session is expired in the database (set after Add to avoid any model defaults)
            var stored = await repo.GetByIdAsync(s.Id);
            // Use DateTime.MinValue to avoid timezone/SQLite conversion ambiguity so the session is unambiguously expired
            stored!.ExpiresAt = DateTime.MinValue;
            await repo.UpdateAsync(stored);

            // Run a single cleanup pass
            await cleanup.CleanupOnceAsync();

            // Verify deletion using a fresh scope so we don't get a tracked entity from the original DbContext
            using var verifyScope = _factory.Services.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var fetched = await verifyRepo.GetByIdAsync(s.Id);
            Assert.Null(fetched);
        }

        [Fact]
        public async Task CleanupOnceAsync_DeletesSessionsWithNullExpiresAt_WhenOlderThan30Minutes()
        {
            using var scope = _factory.Services.CreateScope();
            var services = scope.ServiceProvider;

            var repo = services.GetRequiredService<ISessionRepository>();
            var cleanup = services.GetRequiredService<SessionCleanupService>();

            // Create a session with NULL ExpiresAt, older than DefaultTtlMinutes
            var oldSession = new Session
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddMinutes(-(DefaultTtlMinutes + 5)), // Older than TTL
                ExpiresAt = null, // NULL ExpiresAt (legacy/missed heartbeat)
                Config = new SessionConfig
                {
                    RequireSingerName = false,
                    PauseBetweenSongsSeconds = 0,
                    AllowSingersToReorder = true
                },
                AdminToken = "test-admin-token",
                SingerToken = "test-singer-token"
            };

            await repo.AddAsync(oldSession);

            // Create a recent session with NULL ExpiresAt (should NOT be deleted)
            var recentSession = new Session
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10), // 10 minutes ago
                ExpiresAt = null,
                Config = new SessionConfig
                {
                    RequireSingerName = false,
                    PauseBetweenSongsSeconds = 0,
                    AllowSingersToReorder = true
                },
                AdminToken = "test-admin-token-2",
                SingerToken = "test-singer-token-2"
            };

            await repo.AddAsync(recentSession);

            // Run cleanup
            await cleanup.CleanupOnceAsync();

            // Verify old session with NULL ExpiresAt was deleted
            using var verifyScope = _factory.Services.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var fetchedOld = await verifyRepo.GetByIdAsync(oldSession.Id);
            Assert.Null(fetchedOld);

            // Verify recent session with NULL ExpiresAt was NOT deleted
            var fetchedRecent = await verifyRepo.GetByIdAsync(recentSession.Id);
            Assert.NotNull(fetchedRecent);
        }

        [Fact]
        public async Task CleanupOnceAsync_DeletesAssociatedSongs_WhenSessionExpires()
        {
            using var scope = _factory.Services.CreateScope();
            var services = scope.ServiceProvider;

            var sessionRepo = services.GetRequiredService<ISessionRepository>();
            var songRepo = services.GetRequiredService<ISongRepository>();
            var cleanup = services.GetRequiredService<SessionCleanupService>();

            // Create expired session
            var session = new Session
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
                Config = new SessionConfig
                {
                    RequireSingerName = false,
                    PauseBetweenSongsSeconds = 0,
                    AllowSingersToReorder = true
                },
                AdminToken = "test-admin-token",
                SingerToken = "test-singer-token"
            };

            await sessionRepo.AddAsync(session);

            // Add songs to the session
            var songs = new[]
            {
                new SongUploadDto(Guid.NewGuid(), "Artist A", "Song 1", null),
                new SongUploadDto(Guid.NewGuid(), "Artist B", "Song 2", null),
                new SongUploadDto(Guid.NewGuid(), "Artist C", "Song 3", null)
            };

            await songRepo.BulkUpsertAsync(session.Id, songs);

            // Verify songs were added
            var songsPage = await songRepo.GetPageAsync(session.Id, 1, 10, null, null);
            Assert.Equal(3, songsPage.TotalCount);

            // Run cleanup
            await cleanup.CleanupOnceAsync();

            // Verify session and songs were deleted
            using var verifyScope = _factory.Services.CreateScope();
            var verifySongRepo = verifyScope.ServiceProvider.GetRequiredService<ISongRepository>();
            var songsAfterCleanup = await verifySongRepo.GetPageAsync(session.Id, 1, 10, null, null);
            Assert.Equal(0, songsAfterCleanup.TotalCount);
        }

        [Fact]
        public async Task CleanupOnceAsync_DeletesAssociatedPlaylists_WhenSessionExpires()
        {
            using var scope = _factory.Services.CreateScope();
            var services = scope.ServiceProvider;

            var sessionRepo = services.GetRequiredService<ISessionRepository>();
            var playlistRepo = services.GetRequiredService<IPlaylistRepository>();
            var songRepo = services.GetRequiredService<ISongRepository>();
            var cleanup = services.GetRequiredService<SessionCleanupService>();

            // Create expired session
            var session = new Session
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
                Config = new SessionConfig
                {
                    RequireSingerName = false,
                    PauseBetweenSongsSeconds = 0,
                    AllowSingersToReorder = true
                },
                AdminToken = "test-admin-token",
                SingerToken = "test-singer-token"
            };

            await sessionRepo.AddAsync(session);

            // Add a song to the session
            var songId = Guid.NewGuid();
            await songRepo.BulkUpsertAsync(session.Id, new[]
            {
                new SongUploadDto(songId, "Artist A", "Song 1", null)
            });

            // Get or create playlist for session (uses GetBySessionIdAsync which auto-creates)
            var playlist = await playlistRepo.GetBySessionIdAsync(session.Id);
            Assert.NotNull(playlist);
            Assert.Equal(session.Id, playlist.SessionId);

            // Add an item to the playlist
            playlist.Items.Add(new PlaylistItem
            {
                Id = Guid.NewGuid(),
                PlaylistId = playlist.Id,
                SongId = songId,
                Artist = "Artist A",
                Title = "Song 1",
                SingerName = "Test Singer",
                Position = 0,
                Status = SongStatus.Queued
            });
            await playlistRepo.UpdateAsync(playlist);

            // Verify playlist has items
            var playlistBeforeCleanup = await playlistRepo.GetAsync(playlist.Id);
            Assert.NotNull(playlistBeforeCleanup);
            Assert.Single(playlistBeforeCleanup.Items);

            // Run cleanup
            await cleanup.CleanupOnceAsync();

            // Verify playlist was deleted
            using var verifyScope = _factory.Services.CreateScope();
            var verifyPlaylistRepo = verifyScope.ServiceProvider.GetRequiredService<IPlaylistRepository>();
            var playlistAfterCleanup = await verifyPlaylistRepo.GetAsync(playlist.Id);
            Assert.Null(playlistAfterCleanup);
        }
    }
}
