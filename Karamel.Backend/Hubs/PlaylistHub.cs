using Microsoft.AspNetCore.SignalR;
using Karamel.Backend.Contracts;
using Karamel.Backend.Repositories;
using Karamel.Backend.Models;
using Karamel.Backend.Services;
using System.Diagnostics;
using System.Text.Json;

namespace Karamel.Backend.Hubs
{
    /// <summary>
    /// SignalR hub for real-time playlist synchronization.
    /// Provides mutation methods for playlist management and broadcasts updates to all connected clients in a session.
    /// Authorization enforced inline via X-Link-Token header (validated using ITokenService).
    /// </summary>
    public class PlaylistHub : Hub
    {
        private readonly IPlaylistRepository _playlistRepo;
        private readonly ISessionRepository _sessionRepo;
        private readonly ISongRepository _songRepo;
        private readonly ITokenService _tokenService;
        private readonly ILogger<PlaylistHub> _logger;

        // Per-session semaphores to serialize mutations and avoid races.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> _sessionLocks
            = new();

        private static SemaphoreSlim GetSessionLock(Guid sessionId) =>
            _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));

        public PlaylistHub(IPlaylistRepository playlistRepo, ISessionRepository sessionRepo, ISongRepository songRepo, ITokenService tokenService, ILogger<PlaylistHub> logger)
        {
            _playlistRepo = playlistRepo;
            _sessionRepo = sessionRepo;
            _songRepo = songRepo;
            _tokenService = tokenService;
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
        /// Sends the current playlist state to the newly joined client if session exists.
        /// No authorization required (public method).
        /// </summary>
        public async Task JoinSession(string sessionId)
        {
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                _logger.LogWarning("JoinSession called with invalid session ID: {SessionId}", sessionId);
                throw new HubException("Invalid session ID format");
            }

            // Always add to group (allows future broadcasts even if session doesn't exist yet)
            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(sessionId));
            
            try
            {
                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionGuid);
                var session = await _sessionRepo.GetByIdAsync(sessionGuid);
                
                // Only send initial state if session exists (best-effort initialization)
                if (playlist != null && session != null)
                {
                    var songMetadata = await LoadSongMetadataAsync(sessionGuid, playlist);
                    var dto = BuildPlaylistDto(playlist, session, songMetadata);
                    
                    _logger.LogInformation("Sending initial playlist state to newly joined client for session {SessionId}: {ActiveCount} active items",
                        sessionGuid, dto.Items.Count);
                    
                    await Clients.Caller.SendAsync("ReceivePlaylistUpdated", dto);
                }
                else
                {
                    _logger.LogInformation("Client joined session {SessionId} group, but session/playlist not found (will receive future updates)",
                        sessionGuid);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send initial playlist state to client joining session {SessionId}", sessionGuid);
                // Don't throw - client is already in group and will receive future updates
            }
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
        /// Requires valid X-Link-Token header.
        /// </summary>
        public async Task AddItemAsync(Guid sessionId, Guid songId, string? singerName)
        {
            ValidateToken(sessionId); // any role OK
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
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

                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 3000)
                {
                    _logger.LogWarning("Slow playlist operation detected: AddItemAsync for session {SessionId} took {ElapsedMs}ms",
                        sessionId, stopwatch.ElapsedMilliseconds);
                }

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
        /// Requires valid X-Link-Token header.
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// Uses one-playlist-per-session architecture (playlistId = sessionId).
        /// </summary>
        public async Task RemoveItemAsync(Guid sessionId, Guid itemId)
        {
            ValidateToken(sessionId); // any role OK
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
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

                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 3000)
                {
                    _logger.LogWarning("Slow playlist operation detected: RemoveItemAsync for session {SessionId} took {ElapsedMs}ms",
                        sessionId, stopwatch.ElapsedMilliseconds);
                }

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
        /// Requires valid X-Link-Token header (admin or singer role depending on the operation).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// Uses one-playlist-per-session architecture (playlistId = sessionId).
        /// NOTE: Only reorders items with Status = Queued or UpNext (excludes NowPlaying and Completed).
        /// </summary>
        public async Task ReorderAsync(Guid sessionId, int from, int to)
        {
            ValidateToken(sessionId);
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                _logger.LogInformation("Reordering session {SessionId} playlist: from {From} to {To}", 
                    sessionId, from, to);

                // Get playlist for this session (one playlist per session)
                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);

                // Filter to only active items (Queued and UpNext) - these are the items shown in the UI
                var activeItems = playlist.Items
                    .Where(i => i.Status == SongStatus.Queued || i.Status == SongStatus.UpNext)
                    .OrderBy(i => i.Position)
                    .ToList();

                if (from < 0 || from >= activeItems.Count || to < 0 || to >= activeItems.Count)
                {
                    _logger.LogWarning("Reorder failed: Invalid indices from={From} to={To} for {Count} active items", 
                        from, to, activeItems.Count);
                    throw new HubException("Invalid reorder indices");
                }

                // Reorder within the active items list
                var item = activeItems[from];
                activeItems.RemoveAt(from);
                activeItems.Insert(to, item);

                // Update positions for all active items
                for (int i = 0; i < activeItems.Count; i++)
                {
                    activeItems[i].Position = i;
                }

                // Update the full playlist items list (preserving NowPlaying and Completed items)
                // Remove all active items from playlist and re-add them in new order
                foreach (var activeItem in activeItems)
                {
                    playlist.Items.Remove(activeItem);
                }
                foreach (var activeItem in activeItems)
                {
                    playlist.Items.Add(activeItem);
                }

                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully reordered session {SessionId} playlist", sessionId);

                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 3000)
                {
                    _logger.LogWarning("Slow playlist operation detected: ReorderAsync for session {SessionId} took {ElapsedMs}ms",
                        sessionId, stopwatch.ElapsedMilliseconds);
                }

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
        /// Requires valid X-Link-Token header (admin or singer role depending on the operation).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// </summary>
        public async Task SetSongStatusAsync(Guid sessionId, Guid itemId, int status)
        {
            RequireAdmin(sessionId);
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
        /// Complete the current song: marks current NowPlaying as Completed without advancing to next song.
        /// Requires valid X-Link-Token header (admin or singer role depending on the operation).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// </summary>
        public async Task CompleteCurrentSongAsync(Guid sessionId)
        {
            RequireAdmin(sessionId);
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Completing current song in session {SessionId}", sessionId);

                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);
                
                // Mark current NowPlaying as Completed
                var nowPlaying = playlist.Items.FirstOrDefault(i => i.Status == SongStatus.NowPlaying);
                if (nowPlaying != null)
                {
                    nowPlaying.Status = SongStatus.Completed;
                    nowPlaying.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation("Marked item {ItemId} as Completed in session {SessionId}", nowPlaying.Id, sessionId);
                }
                else
                {
                    _logger.LogWarning("No NowPlaying song found to complete in session {SessionId}", sessionId);
                }

                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully completed current song in session {SessionId}", sessionId);

                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing current song in session {SessionId}", sessionId);
                throw new HubException("Failed to complete current song");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Advance to the next song: marks current NowPlaying as Completed, marks first UpNext as NowPlaying.
        /// Requires valid X-Link-Token header (admin or singer role depending on the operation).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// </summary>
        public async Task AdvanceToNextSongAsync(Guid sessionId)
        {
            RequireAdmin(sessionId);
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                _logger.LogInformation("Advancing to next song in session {SessionId}", sessionId);

                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    throw new HubException("Session not found");
                }

                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);
                
                // Mark current NowPlaying as Completed
                var nowPlaying = playlist.Items.FirstOrDefault(i => i.Status == SongStatus.NowPlaying);
                if (nowPlaying != null)
                {
                    nowPlaying.Status = SongStatus.Completed;
                    nowPlaying.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation("Marked item {ItemId} as Completed in session {SessionId}", nowPlaying.Id, sessionId);
                }

                // Check PlaybackMode and handle transitions
                if (session.Config.PlaybackMode == PlaybackMode.StopAfterCurrent)
                {
                    // Transition to Stopped state - do not advance to next song
                    session.Config.PlaybackMode = PlaybackMode.Stopped;
                    await _sessionRepo.UpdateAsync(session);
                    _logger.LogInformation("Session {SessionId} entered Stopped state after current song", sessionId);
                }
                else if (session.Config.PlaybackMode == PlaybackMode.Normal)
                {
                    // Normal playback - mark first Queued (or UpNext) as NowPlaying
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
                }
                // If PlaybackMode.Stopped, do nothing (stay stopped)

                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully advanced song in session {SessionId}", sessionId);

                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 3000)
                {
                    _logger.LogWarning("Slow playlist operation detected: AdvanceToNextSongAsync for session {SessionId} took {ElapsedMs}ms",
                        sessionId, stopwatch.ElapsedMilliseconds);
                }

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
        /// Set the PlaybackMode to StopAfterCurrent, indicating playback should stop after the current song finishes.
        /// Requires valid X-Link-Token header (admin or singer role depending on the operation).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// </summary>
        public async Task SetStopAfterCurrentAsync(Guid sessionId)
        {
            RequireAdmin(sessionId);
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Setting StopAfterCurrent mode for session {SessionId}", sessionId);

                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    throw new HubException("Session not found");
                }

                session.Config.PlaybackMode = PlaybackMode.StopAfterCurrent;
                await _sessionRepo.UpdateAsync(session);

                _logger.LogInformation("Session {SessionId} set to StopAfterCurrent mode", sessionId);

                // Broadcast update to notify all clients of mode change
                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);
                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting StopAfterCurrent mode for session {SessionId}", sessionId);
                throw new HubException("Failed to set stop after current mode");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Proceed with playback from Stopped state, advancing to the next song and setting mode to Normal.
        /// Requires valid X-Link-Token header (admin or singer role depending on the operation).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// </summary>
        public async Task ProceedPlaybackAsync(Guid sessionId)
        {
            RequireAdmin(sessionId);
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Proceeding playback from Stopped state for session {SessionId}", sessionId);

                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    throw new HubException("Session not found");
                }

                if (session.Config.PlaybackMode != PlaybackMode.Stopped)
                {
                    _logger.LogWarning("ProceedPlayback called but session {SessionId} is not in Stopped state (current: {Mode})",
                        sessionId, session.Config.PlaybackMode);
                    throw new HubException("Session is not in stopped state");
                }

                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);

                // Advance to next song
                var nextSong = playlist.Items
                    .Where(i => i.Status == SongStatus.Queued || i.Status == SongStatus.UpNext)
                    .OrderBy(i => i.Position)
                    .FirstOrDefault();
                
                if (nextSong != null)
                {
                    nextSong.Status = SongStatus.NowPlaying;
                    await _playlistRepo.UpdateAsync(playlist);
                    _logger.LogInformation("Advanced item {ItemId} to NowPlaying in session {SessionId}", nextSong.Id, sessionId);
                }
                else
                {
                    _logger.LogWarning("No queued song found to proceed playback in session {SessionId}", sessionId);
                    throw new HubException("No songs in queue to play");
                }

                // Set mode back to Normal
                session.Config.PlaybackMode = PlaybackMode.Normal;
                await _sessionRepo.UpdateAsync(session);

                _logger.LogInformation("Session {SessionId} resumed playback to Normal mode", sessionId);

                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proceeding playback for session {SessionId}", sessionId);
                throw new HubException("Failed to proceed playback");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Clear all queued and up-next songs from the playlist, preserving the currently playing song.
        /// Requires valid X-Link-Token header (admin or singer role depending on the operation).
        /// Broadcasts ReceivePlaylistUpdated to all clients in the session group.
        /// </summary>
        public async Task ClearQueueAsync(Guid sessionId)
        {
            RequireAdmin(sessionId);
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                _logger.LogInformation("Clearing queue (Queued and UpNext songs) in session {SessionId}", sessionId);

                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);
                
                // Get items to remove (Queued and UpNext, but NOT NowPlaying or Completed)
                var itemsToRemove = playlist.Items
                    .Where(i => i.Status == SongStatus.Queued || i.Status == SongStatus.UpNext)
                    .ToList();
                
                // Log items before batch removal for audit trail
                LogPlaylistItemsRemoval(itemsToRemove, sessionId);

                // Use RemoveAll for efficient batch deletion instead of removing one by one
                playlist.Items.RemoveAll(i => i.Status == SongStatus.Queued || i.Status == SongStatus.UpNext);

                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully cleared {Count} queued songs from session {SessionId}", itemsToRemove.Count, sessionId);

                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 3000)
                {
                    _logger.LogWarning("Slow playlist operation detected: ClearQueueAsync for session {SessionId} took {ElapsedMs}ms",
                        sessionId, stopwatch.ElapsedMilliseconds);
                }

                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing queue in session {SessionId}", sessionId);
                throw new HubException("Failed to clear queue");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Pause the session: broadcasts ReceiveSessionPaused to all clients in the session group.
        /// The paused flag is transient (frontend-only); no database change is made.
        /// Requires admin token (enforced by LinkTokenHubFilter).
        /// </summary>
        public async Task PauseSessionAsync(Guid sessionId)
        {
            try
            {
                RequireAdmin(sessionId);
                _logger.LogInformation("Pausing session {SessionId}", sessionId);

                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("PauseSession failed: Session {SessionId} not found", sessionId);
                    throw new HubException("Session not found");
                }

                var groupName = GetSessionGroupName(sessionId.ToString());
                await Clients.Group(groupName).SendAsync("ReceiveSessionPaused");

                _logger.LogInformation("Session {SessionId} paused – broadcast sent to group", sessionId);
            }
            catch (HubException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing session {SessionId}", sessionId);
                throw new HubException("Failed to pause session");
            }
        }

        /// <summary>
        /// Resume the session: broadcasts ReceiveSessionResumed to all clients in the session group.
        /// The paused flag is transient (frontend-only); no database change is made.
        /// Requires admin token (enforced by LinkTokenHubFilter).
        /// </summary>
        public async Task ResumeSessionAsync(Guid sessionId)
        {
            try
            {
                RequireAdmin(sessionId);
                _logger.LogInformation("Resuming session {SessionId}", sessionId);

                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("ResumeSession failed: Session {SessionId} not found", sessionId);
                    throw new HubException("Session not found");
                }

                var groupName = GetSessionGroupName(sessionId.ToString());
                await Clients.Group(groupName).SendAsync("ReceiveSessionResumed");

                _logger.LogInformation("Session {SessionId} resumed – broadcast sent to group", sessionId);
            }
            catch (HubException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming session {SessionId}", sessionId);
                throw new HubException("Failed to resume session");
            }
        }

        /// <summary>
        /// Update runtime session configuration. Persists changes to the database and
        /// broadcasts ReceiveConfigUpdated with the updated config to all clients.
        /// Requires admin token (enforced by LinkTokenHubFilter).
        /// </summary>
        public async Task UpdateSessionConfigAsync(Guid sessionId, SessionConfigDto config)
        {
            try
            {
                RequireAdmin(sessionId);
                _logger.LogInformation(
                    "Updating session config for {SessionId}: RequireSingerName={RequireSingerName}, AllowSingersToReorder={AllowSingersToReorder}, PauseBetweenSongsSeconds={PauseBetweenSongsSeconds}, Theme={Theme}",
                    sessionId, config.RequireSingerName, config.AllowSingersToReorder, config.PauseBetweenSongsSeconds, config.Theme);

                if (config.PauseBetweenSongsSeconds < 0)
                {
                    _logger.LogWarning("UpdateSessionConfig rejected for session {SessionId}: PauseBetweenSongsSeconds={Value} is negative",
                        sessionId, config.PauseBetweenSongsSeconds);
                    throw new HubException("PauseBetweenSongsSeconds must be non-negative");
                }

                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("UpdateSessionConfig failed: Session {SessionId} not found", sessionId);
                    throw new HubException("Session not found");
                }

                // Apply only the config fields exposed in the DTO; preserve PlaybackMode.
                session.Config.RequireSingerName = config.RequireSingerName;
                session.Config.AllowSingersToReorder = config.AllowSingersToReorder;
                session.Config.PauseBetweenSongsSeconds = config.PauseBetweenSongsSeconds;
                session.Config.Theme = config.Theme;

                await _sessionRepo.UpdateAsync(session);

                _logger.LogInformation("Session config persisted for {SessionId}", sessionId);

                // Broadcast updated config to all clients
                var updatedDto = SessionConfigDto.FromModel(session.Config);
                var groupName = GetSessionGroupName(sessionId.ToString());
                await Clients.Group(groupName).SendAsync("ReceiveConfigUpdated", updatedDto);

                _logger.LogInformation("ReceiveConfigUpdated broadcast sent for session {SessionId}", sessionId);
            }
            catch (HubException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session config for {SessionId}", sessionId);
                throw new HubException("Failed to update session config");
            }
        }

        /// <summary>
        /// Builds a PlaylistUpdatedDto from the current playlist and session state.
        /// Filters out Completed items, includes CurrentSong (first NowPlaying item).
        /// Read-only operation - does NOT modify playlist state.
        /// </summary>
        private PlaylistUpdatedDto BuildPlaylistDto(Playlist playlist, Session session, Dictionary<Guid, string?> songMetadata)
        {
            // Filter out Completed and NowPlaying items (NowPlaying goes to CurrentSong)
            var activeItems = playlist.Items
                .Where(i => i.Status != SongStatus.Completed && i.Status != SongStatus.NowPlaying)
                .OrderBy(i => i.Position)
                .ToList();
            
            // Get CurrentSong (first NowPlaying item, or null)
            var currentSongItem = playlist.Items.FirstOrDefault(i => i.Status == SongStatus.NowPlaying);
            PlaylistItemDto? currentSong = currentSongItem != null 
                ? new PlaylistItemDto(
                    currentSongItem.Id,
                    currentSongItem.Artist,
                    currentSongItem.Title,
                    currentSongItem.SingerName,
                    currentSongItem.Position,
                    currentSongItem.SongId,
                    (int)currentSongItem.Status,
                    ParseDuration(currentSongItem.SongId.HasValue ? songMetadata.GetValueOrDefault(currentSongItem.SongId.Value) : null))
                : null;
            
            return new PlaylistUpdatedDto(
                playlist.Id,
                playlist.SessionId,
                activeItems.Select(i => new PlaylistItemDto(
                    i.Id,
                    i.Artist,
                    i.Title,
                    i.SingerName,
                    i.Position,
                    i.SongId,
                    (int)i.Status,
                    ParseDuration(i.SongId.HasValue ? songMetadata.GetValueOrDefault(i.SongId.Value) : null)
                )).ToList(),
                currentSong,
                (int)session.Config.PlaybackMode
            );
        }

        /// <summary>
        /// Parses durationSeconds from a song's MetadataJson. Returns 0 if absent or invalid.
        /// </summary>
        private static int ParseDuration(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson)) return 0;
            try
            {
                using var doc = JsonDocument.Parse(metadataJson);
                return doc.RootElement.TryGetProperty("durationSeconds", out var p)
                       && p.TryGetInt32(out var d) ? d : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Loads MetadataJson for all songs referenced by playlist items.
        /// Returns a dictionary from SongId to MetadataJson (null means song not found or no metadata).
        /// </summary>
        private async Task<Dictionary<Guid, string?>> LoadSongMetadataAsync(Guid sessionId, Playlist playlist)
        {
            var result = new Dictionary<Guid, string?>();
            var songIds = playlist.Items
                .Where(i => i.SongId.HasValue)
                .Select(i => i.SongId!.Value)
                .Distinct();

            foreach (var songId in songIds)
            {
                var song = await _songRepo.GetByIdAsync(sessionId, songId);
                result[songId] = song?.MetadataJson;
            }
            return result;
        }

        /// <summary>
        /// Helper method to broadcast playlist updates to all clients in a session group.
        /// Auto-promotes first Queued song to UpNext when queue needs a next song.
        /// </summary>
        private async Task BroadcastPlaylistUpdate(Guid sessionId, Playlist playlist)
        {
            var groupName = GetSessionGroupName(sessionId.ToString());
            
            // Load session to get PlaybackMode
            var session = await _sessionRepo.GetByIdAsync(sessionId);
            if (session == null)
            {
                _logger.LogError("Session {SessionId} not found during BroadcastPlaylistUpdate", sessionId);
                return;
            }
            
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
            
            // Build DTO and broadcast to all clients in session
            var songMetadata = await LoadSongMetadataAsync(sessionId, playlist);
            var dto = BuildPlaylistDto(playlist, session, songMetadata);
            
            // Log current playlist state for diagnostics
            _logger.LogInformation("Broadcasting playlist update for session {SessionId}: {ActiveCount} active items (Queued: {QueuedCount}, UpNext: {UpNextCount}), CurrentSong: {HasCurrentSong}",
                sessionId,
                dto.Items.Count,
                dto.Items.Count(i => i.Status == (int)SongStatus.Queued),
                dto.Items.Count(i => i.Status == (int)SongStatus.UpNext),
                dto.CurrentSong != null ? $"{dto.CurrentSong.Artist} - {dto.CurrentSong.Title}" : "None");

            await Clients.Group(groupName).SendAsync("ReceivePlaylistUpdated", dto);
        }

        /// <summary>
        /// Logs the removal of playlist items in a single efficient log entry.
        /// </summary>
        private void LogPlaylistItemsRemoval(List<PlaylistItem> items, Guid sessionId)
        {
            if (items.Count == 0)
            {
                _logger.LogInformation("No items to remove from session {SessionId}", sessionId);
                return;
            }

            // Log first 5 items as examples for audit trail
            var sampleItems = items.Take(5)
                .Select(i => $"{i.Status}:{i.Artist}-{i.Title}")
                .ToList();
            
            var summary = items.Count <= 5 
                ? string.Join(", ", sampleItems)
                : $"{string.Join(", ", sampleItems)} and {items.Count - 5} more";

            _logger.LogInformation("Removing {Count} items from session {SessionId}: {Summary}",
                items.Count, sessionId, summary);
        }

        /// <summary>
        /// Validates the X-Link-Token header for the current connection against the given session.
        /// Returns the role ("admin" or "singer") if valid, or throws HubException.
        /// </summary>
        private string ValidateToken(Guid sessionId)
        {
            var token = Context.Items.TryGetValue("X-Link-Token", out var t) ? t?.ToString() : null;
            if (string.IsNullOrEmpty(token))
                throw new HubException("Missing X-Link-Token header");
            var (role, isValid) = _tokenService.ValidateToken(token, sessionId);
            if (!isValid)
                throw new HubException("Invalid or expired link token");
            return role;
        }

        /// <summary>
        /// Validates the token and ensures the caller has the admin role.
        /// Throws HubException if not authenticated or not admin.
        /// </summary>
        private void RequireAdmin(Guid sessionId)
        {
            var role = ValidateToken(sessionId);
            if (role != "admin")
                throw new HubException("This operation requires admin permissions");
        }

        /// <summary>
        /// Get the SignalR group name for a session.
        /// </summary>
        public static string GetSessionGroupName(string sessionId) => $"session-{sessionId}";

        /// <summary>
        /// SignalR RPC to retrieve a paginated library page. No mutation performed.
        /// Requires the caller to be connected; authorization for read is allowed without link token but callers should provide a valid session context.
        /// </summary>
        public async Task<object> GetLibraryPage(Guid sessionId, int page = 1, int pageSize = 50, string? search = null, string? sort = null, string? artist = null)
        {
            var result = await _songRepo.GetPageAsync(sessionId, page, pageSize, search, sort, artist);
            return new { items = result.Items, page = result.Page, pageSize = result.PageSize, totalCount = result.TotalCount, suggestions = result.Suggestions };
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
    public record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position, Guid? SongId, int Status, int DurationSeconds = 0);
    public record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, List<PlaylistItemDto> Items, PlaylistItemDto? CurrentSong, int PlaybackMode);
}
