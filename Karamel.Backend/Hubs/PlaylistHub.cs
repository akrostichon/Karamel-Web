using Microsoft.AspNetCore.SignalR;
using Karamel.Backend.Repositories;
using Karamel.Backend.Models;

namespace Karamel.Backend.Hubs
{
    /// <summary>
    /// SignalR hub for real-time playlist synchronization.
    /// Provides mutation methods for playlist management and broadcasts updates to all connected clients in a session.
    /// Authorization enforced via LinkTokenHubFilter (X-Link-Token header required for mutations).
    /// </summary>
    public class PlaylistHub : Hub
    {
        private readonly IPlaylistRepository _playlistRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly Karamel.Backend.Repositories.ISongRepository _songRepo;
        private readonly ILogger<PlaylistHub> _logger;

        // Per-session semaphores to serialize mutations and avoid races.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, System.Threading.SemaphoreSlim> _sessionLocks
            = new();

        private static System.Threading.SemaphoreSlim GetSessionLock(Guid sessionId) =>
            _sessionLocks.GetOrAdd(sessionId, _ => new System.Threading.SemaphoreSlim(1, 1));

        public PlaylistHub(IPlaylistRepository playlistRepo, ISessionRepository sessionRepo, Karamel.Backend.Repositories.ISongRepository songRepo, ILogger<PlaylistHub> logger)
        {
            _playlistRepo = playlistRepo;
            _sessionRepo = sessionRepo;
            _songRepo = songRepo;
            _logger = logger;
        }

        /// <summary>
        /// Called when a new connection is established.
        /// Stores the X-Link-Token from headers in connection context for later validation.
        /// </summary>
        public override Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                var token = httpContext.Request.Headers["X-Link-Token"].FirstOrDefault();
                if (string.IsNullOrEmpty(token))
                {
                    token = httpContext.Request.Query["access_token"].FirstOrDefault();
                }
                if (!string.IsNullOrEmpty(token))
                {
                    Context.Items["X-Link-Token"] = token;
                }
            }
            return base.OnConnectedAsync();
        }

        /// <summary>
        /// Join a session group to receive real-time playlist updates.
        /// No authorization required (public method).
        /// </summary>
        public async Task JoinSession(string sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(sessionId));
        }

        /// <summary>
        /// Leave a session group.
        /// No authorization required (public method).
        /// </summary>
        public async Task LeaveSession(string sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetSessionGroupName(sessionId));
        }

        /// <summary>
        /// Add a song to the playlist.
        /// Requires valid X-Link-Token header (validated by LinkTokenHubFilter).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// Uses one-playlist-per-session architecture (playlistId = sessionId).
        /// </summary>
        public async Task AddItemAsync(Guid sessionId, Guid songId, string? singerName)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Adding item to session {SessionId}: SongId={SongId} (Singer: {SingerName})", 
                    sessionId, songId, singerName ?? "None");

                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("AddItem failed: Session {SessionId} not found", sessionId);
                    throw new HubException("Session not found");
                }

                // Get or create playlist for this session (one playlist per session)
                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);

                // Lookup song by ID to get Artist/Title
                var song = await _songRepo.GetByIdAsync(sessionId, songId);
                if (song == null)
                {
                    _logger.LogWarning("AddItem failed: Song {SongId} not found in session {SessionId}", songId, sessionId);
                    throw new HubException("Song not found in session library");
                }

                _logger.LogInformation("Adding item to playlist {PlaylistId} in session {SessionId}: {SongId}(Singer: {SingerName})", 
                    playlist.Id, sessionId, songId, singerName ?? "None");

                var item = new PlaylistItem
                {
                    Id = Guid.NewGuid(),
                    PlaylistId = playlist.Id,
                    Position = playlist.Items.Count,
                    Artist = song.Artist,
                    Title = song.Title,
                    SingerName = singerName,
                    SongId = songId,
                    Status = SongStatus.Queued  // NEW: Explicit initial status
                };

                playlist.Items.Add(item);
                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully added item {ItemId} to session {SessionId} playlist", item.Id, sessionId);

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (HubException)
            {
                throw; // Re-throw HubException without additional logging
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to session {SessionId} playlist", sessionId);
                throw new HubException("Failed to add item to playlist");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Remove a song from the playlist.
        /// Requires valid X-Link-Token header (validated by LinkTokenHubFilter).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// Uses one-playlist-per-session architecture (playlistId = sessionId).
        /// </summary>
        public async Task RemoveItemAsync(Guid sessionId, Guid itemId)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Removing item {ItemId} from session {SessionId} playlist", 
                    itemId, sessionId);

                // Get playlist for this session (one playlist per session)
                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);

                var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
                if (item == null)
                {
                    _logger.LogWarning("RemoveItem failed: Item {ItemId} not found in session {SessionId} playlist", itemId, sessionId);
                    throw new HubException("Item not found in playlist");
                }

                playlist.Items.Remove(item);

                // Re-index positions
                for (int i = 0; i < playlist.Items.Count; i++)
                {
                    playlist.Items[i].Position = i;
                }

                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully removed item {ItemId} from session {SessionId} playlist", itemId, sessionId);

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (HubException)
            {
                throw; // Re-throw HubException without additional logging
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item {ItemId} from session {SessionId} playlist", 
                    itemId, sessionId);
                throw new HubException("Failed to remove item from playlist");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Reorder songs in the playlist.
        /// Requires valid X-Link-Token header (validated by LinkTokenHubFilter).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// Uses one-playlist-per-session architecture (playlistId = sessionId).
        /// </summary>
        public async Task ReorderAsync(Guid sessionId, int from, int to)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Reordering session {SessionId} playlist: from {From} to {To}", 
                    sessionId, from, to);

                // Get playlist for this session (one playlist per session)
                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);

                if (from < 0 || from >= playlist.Items.Count || to < 0 || to >= playlist.Items.Count)
                {
                    _logger.LogWarning("Reorder failed: Invalid indices from={From} to={To} for playlist with {Count} items", 
                        from, to, playlist.Items.Count);
                    throw new HubException("Invalid reorder indices");
                }

                var item = playlist.Items[from];
                playlist.Items.RemoveAt(from);
                playlist.Items.Insert(to, item);

                // Re-index all positions
                for (int i = 0; i < playlist.Items.Count; i++)
                {
                    playlist.Items[i].Position = i;
                }

                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully reordered session {SessionId} playlist", sessionId);

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (HubException)
            {
                throw; // Re-throw HubException without additional logging
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering session {SessionId} playlist", sessionId);
                throw new HubException("Failed to reorder playlist");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Set the status of a specific playlist item.
        /// Requires valid X-Link-Token header (validated by LinkTokenHubFilter).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// </summary>
        public async Task SetSongStatusAsync(Guid sessionId, Guid itemId, int status)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                var songStatus = (SongStatus)status;
                _logger.LogInformation("Setting status for item {ItemId} in session {SessionId} to {Status}", 
                    itemId, sessionId, songStatus);

                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);
                var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
                
                if (item == null)
                {
                    _logger.LogWarning("SetSongStatus failed: Item {ItemId} not found in session {SessionId}", itemId, sessionId);
                    throw new HubException("Item not found in playlist");
                }

                item.Status = songStatus;
                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully set item {ItemId} status to {Status} in session {SessionId}", itemId, songStatus, sessionId);

                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (HubException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting status for item {ItemId} in session {SessionId}", itemId, sessionId);
                throw new HubException("Failed to set song status");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Advance to the next song: marks current NowPlaying as Completed, marks first UpNext as NowPlaying.
        /// Requires valid X-Link-Token header (validated by LinkTokenHubFilter).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// </summary>
        public async Task AdvanceToNextSongAsync(Guid sessionId)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Advancing to next song in session {SessionId}", sessionId);

                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);
                
                // Mark current NowPlaying as Completed
                var nowPlaying = playlist.Items.FirstOrDefault(i => i.Status == SongStatus.NowPlaying);
                if (nowPlaying != null)
                {
                    nowPlaying.Status = SongStatus.Completed;
                    nowPlaying.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation("Marked item {ItemId} as Completed in session {SessionId}", nowPlaying.Id, sessionId);
                }

                // Mark first Queued (or UpNext) as NowPlaying
                var nextSong = playlist.Items
                    .Where(i => i.Status == SongStatus.Queued || i.Status == SongStatus.UpNext)
                    .OrderBy(i => i.Position)
                    .FirstOrDefault();
                
                if (nextSong != null)
                {
                    nextSong.Status = SongStatus.NowPlaying;
                    _logger.LogInformation("Advanced item {ItemId} to NowPlaying in session {SessionId}", nextSong.Id, sessionId);
                }
                else
                {
                    _logger.LogInformation("No queued song found to advance in session {SessionId}", sessionId);
                }

                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully advanced song in session {SessionId}", sessionId);

                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error advancing song in session {SessionId}", sessionId);
                throw new HubException("Failed to advance to next song");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Helper method to broadcast playlist updates to all clients in a session group.
        /// Filters out Completed items and includes CurrentSong (first NowPlaying item).
        /// Auto-promotes first Queued song to UpNext when queue needs a next song.
        /// </summary>
        private async Task BroadcastPlaylistUpdate(Guid sessionId, Playlist playlist)
        {
            var groupName = GetSessionGroupName(sessionId.ToString());
            
            // Auto-promote first Queued to UpNext if needed
            // Promote whenever there's no UpNext (works during active playback and when idle)
            var hasUpNext = playlist.Items.Any(i => i.Status == SongStatus.UpNext);
            var firstQueued = playlist.Items
                .Where(i => i.Status == SongStatus.Queued)
                .OrderBy(i => i.Position)
                .FirstOrDefault();

            if (!hasUpNext && firstQueued != null)
            {
                firstQueued.Status = SongStatus.UpNext;
                await _playlistRepo.UpdateAsync(playlist);
                _logger.LogInformation("Auto-promoted item {ItemId} ('{Artist} - {Title}') from Queued to UpNext in session {SessionId}",
                    firstQueued.Id, firstQueued.Artist, firstQueued.Title, sessionId);
            }
            
            // Filter out Completed and NowPlaying items (NowPlaying goes to CurrentSong)
            var activeItems = playlist.Items
                .Where(i => i.Status != SongStatus.Completed && i.Status != SongStatus.NowPlaying)
                .OrderBy(i => i.Position)
                .ToList();
            
            // Log current playlist state for diagnostics
            _logger.LogInformation("Broadcasting playlist update for session {SessionId}: {ActiveCount} active items (Queued: {QueuedCount}, UpNext: {UpNextCount}, NowPlaying: {NowPlayingCount})",
                sessionId,
                activeItems.Count,
                activeItems.Count(i => i.Status == SongStatus.Queued),
                activeItems.Count(i => i.Status == SongStatus.UpNext),
                activeItems.Count(i => i.Status == SongStatus.NowPlaying));
            
            // Get CurrentSong (first NowPlaying item, or null)
            var currentSongItem = playlist.Items.FirstOrDefault(i => i.Status == SongStatus.NowPlaying);
            if (currentSongItem != null)
            {
                _logger.LogInformation("Current song in session {SessionId}: {ItemId} ('{Artist} - {Title}')",
                    sessionId, currentSongItem.Id, currentSongItem.Artist, currentSongItem.Title);
            }
            PlaylistItemDto? currentSong = currentSongItem != null 
                ? new PlaylistItemDto(
                    currentSongItem.Id,
                    currentSongItem.Artist,
                    currentSongItem.Title,
                    currentSongItem.SingerName,
                    currentSongItem.Position,
                    currentSongItem.SongId,
                    (int)currentSongItem.Status)
                : null;
            
            var dto = new PlaylistUpdatedDto(
                playlist.Id,
                playlist.SessionId,
                activeItems.Select(i => new PlaylistItemDto(
                    i.Id,
                    i.Artist,
                    i.Title,
                    i.SingerName,
                    i.Position,
                    i.SongId,
                    (int)i.Status  // Include Status
                )).ToList(),
                currentSong  // Include CurrentSong
            );

            await Clients.Group(groupName).SendAsync("ReceivePlaylistUpdated", dto);
        }

        /// <summary>
        /// Get the SignalR group name for a session.
        /// </summary>
        public static string GetSessionGroupName(string sessionId) => $"session-{sessionId}";

        /// <summary>
        /// SignalR RPC to retrieve a paginated library page. No mutation performed.
        /// Requires the caller to be connected; authorization for read is allowed without link token but callers should provide a valid session context.
        /// </summary>
        public async Task<object> GetLibraryPage(Guid sessionId, int page = 1, int pageSize = 50, string? search = null, string? sort = null)
        {
            var result = await _songRepo.GetPageAsync(sessionId, page, pageSize, search, sort);
            return new { items = result.Items, page = result.Page, pageSize = result.PageSize, totalCount = result.TotalCount };
        }

        /// <summary>
        /// SignalR RPC to perform quick search (autocomplete) returning up to maxResults items.
        /// </summary>
        public async Task<IEnumerable<object>> SearchLibrary(Guid sessionId, string query, int maxResults = 20)
        {
            var page = await _songRepo.GetPageAsync(sessionId, 1, maxResults, query, "artist");
            return page.Items.Select(i => new { i.Id, i.Artist, i.Title, i.MetadataJson });
        }
    }

    // DTOs for hub payloads (shared with controller for now)
    public record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position, Guid? SongId, int Status);
    public record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, System.Collections.Generic.List<PlaylistItemDto> Items, PlaylistItemDto? CurrentSong);
}
