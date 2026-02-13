using Microsoft.AspNetCore.SignalR;
using static Karamel.Backend.Models.SessionConstants;

namespace Karamel.Backend.Services
{
    /// <summary>
    /// Background service that periodically removes expired sessions and notifies connected clients.
    /// The core cleanup logic is exposed via <see cref="CleanupOnceAsync"/> to allow deterministic testing.
    /// </summary>
    public class SessionCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<SessionCleanupService> _logger;
        private readonly TimeSpan _interval;

        public SessionCleanupService(IServiceProvider services, ILogger<SessionCleanupService> logger)
        {
            _services = services;
            _logger = logger;
            // Default run interval: 1 minute
            _interval = TimeSpan.FromMinutes(1);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionCleanupService started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while running session cleanup");
                }

                await Task.Delay(_interval, stoppingToken);
            }
            _logger.LogInformation("SessionCleanupService stopping");
        }

        /// <summary>
        /// Performs one cleanup pass. This method is public to allow unit/integration tests to invoke cleanup deterministically.
        /// It finds sessions with ExpiresAt <= UtcNow OR NULL ExpiresAt older than DefaultTtlMinutes, deletes associated data (songs/playlists),
        /// and then deletes the session. For each deleted session it broadcasts a "ReceiveSessionEnded" message to the SignalR group 
        /// so clients can handle termination gracefully.
        /// </summary>
        public async Task CleanupOnceAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Repositories.ISessionRepository>();
            var songRepo = scope.ServiceProvider.GetRequiredService<Repositories.ISongRepository>();
            var playlistRepo = scope.ServiceProvider.GetRequiredService<Repositories.IPlaylistRepository>();
            var hubContext = scope.ServiceProvider.GetService<IHubContext<Hubs.PlaylistHub>>();

            var now = DateTime.UtcNow;
            var sessions = await repo.ListAsync();
            
            // Catch sessions with explicit expiry OR NULL ExpiresAt older than DefaultTtlMinutes (legacy/missed heartbeat sessions)
            var expiredSessions = sessions.Where(s => 
                (s.ExpiresAt.HasValue && s.ExpiresAt.Value <= now) ||
                (!s.ExpiresAt.HasValue && s.CreatedAt < now.AddMinutes(-DefaultTtlMinutes))
            ).ToList();

            int sessionsDeleted = 0, songsDeleted = 0, playlistsDeleted = 0;

            foreach (var expiredSession in expiredSessions)
            {
                try
                {
                    _logger.LogInformation("Expiring session {SessionId} (ExpiresAt={ExpiresAt}, CreatedAt={CreatedAt})", 
                        expiredSession.Id, expiredSession.ExpiresAt, expiredSession.CreatedAt);
                    
                    // Explicit cleanup of associated data (belt-and-suspenders before FK cascade added in Phase 3)
                    try
                    {
                        await songRepo.DeleteBySessionAsync(expiredSession.Id);
                        songsDeleted++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete songs for session {SessionId}", expiredSession.Id);
                    }

                    try
                    {
                        var playlist = await playlistRepo.GetBySessionIdAsync(expiredSession.Id);
                        if (playlist != null)
                        {
                            await playlistRepo.DeleteAsync(playlist.Id);
                            playlistsDeleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete playlist for session {SessionId}", expiredSession.Id);
                    }

                    // Delete session itself
                    await repo.DeleteAsync(expiredSession.Id);
                    sessionsDeleted++;

                    if (hubContext != null)
                    {
                        var group = Hubs.PlaylistHub.GetSessionGroupName(expiredSession.Id.ToString());
                        await hubContext.Clients.Group(group).SendAsync("ReceiveSessionEnded", expiredSession.Id, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to expire session {SessionId}", expiredSession.Id);
                }
            }

            if (sessionsDeleted > 0)
            {
                _logger.LogInformation("Cleanup complete: {SessionsDeleted} sessions, {SongsDeleted} song collections, {PlaylistsDeleted} playlists deleted",
                    sessionsDeleted, songsDeleted, playlistsDeleted);
            }
        }
    }
}
