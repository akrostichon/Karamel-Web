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
        /// </summary>
        public async Task AddItemAsync(Guid sessionId, string artist, string title, string? singerName)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("AddItem failed: Session {SessionId} not found", sessionId);
                    throw new HubException("Session not found");
                }

                // Look up the playlist for this session (each session has exactly one playlist)
                var playlist = await _playlistRepo.GetBySessionIdAsync(sessionId);
                if (playlist == null)
                {
                    _logger.LogWarning("AddItem failed: Playlist not found for session {SessionId}", sessionId);
                    throw new HubException("Playlist not found for session");
                }

                _logger.LogInformation("Adding item to playlist {PlaylistId} in session {SessionId}: {Artist} - {Title} (Singer: {SingerName})", 
                    playlist.Id, sessionId, artist, title, singerName ?? "None");

                var item = new PlaylistItem
                {
                    Id = Guid.NewGuid(),
                    PlaylistId = playlist.Id,
                    Position = playlist.Items.Count,
                    Artist = artist,
                    Title = title,
                    SingerName = singerName
                };

                playlist.Items.Add(item);
                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully added item {ItemId} to playlist {PlaylistId}", item.Id, playlist.Id);

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (HubException)
            {
                throw; // Re-throw HubException without additional logging
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to playlist in session {SessionId}", sessionId);
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
        /// </summary>
        public async Task RemoveItemAsync(Guid sessionId, Guid playlistId, Guid itemId)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Removing item {ItemId} from playlist {PlaylistId} in session {SessionId}", 
                    itemId, playlistId, sessionId);

                var playlist = await _playlistRepo.GetAsync(playlistId);
                if (playlist == null || playlist.SessionId != sessionId)
                {
                    _logger.LogWarning("RemoveItem failed: Playlist {PlaylistId} not found or does not belong to session {SessionId}", 
                        playlistId, sessionId);
                    throw new HubException("Playlist not found or does not belong to session");
                }

                var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
                if (item == null)
                {
                    _logger.LogWarning("RemoveItem failed: Item {ItemId} not found in playlist {PlaylistId}", itemId, playlistId);
                    throw new HubException("Item not found in playlist");
                }

                playlist.Items.Remove(item);

                // Re-index positions
                for (int i = 0; i < playlist.Items.Count; i++)
                {
                    playlist.Items[i].Position = i;
                }

                await _playlistRepo.UpdateAsync(playlist);

                _logger.LogInformation("Successfully removed item {ItemId} from playlist {PlaylistId}", itemId, playlistId);

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (HubException)
            {
                throw; // Re-throw HubException without additional logging
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item {ItemId} from playlist {PlaylistId} in session {SessionId}", 
                    itemId, playlistId, sessionId);
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
        /// </summary>
        public async Task ReorderAsync(Guid sessionId, Guid playlistId, int from, int to)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                _logger.LogInformation("Reordering playlist {PlaylistId} in session {SessionId}: from {From} to {To}", 
                    playlistId, sessionId, from, to);

                var playlist = await _playlistRepo.GetAsync(playlistId);
                if (playlist == null || playlist.SessionId != sessionId)
                {
                    _logger.LogWarning("Reorder failed: Playlist {PlaylistId} not found or does not belong to session {SessionId}", 
                        playlistId, sessionId);
                    throw new HubException("Playlist not found or does not belong to session");
                }

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

                _logger.LogInformation("Successfully reordered playlist {PlaylistId}", playlistId);

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
            }
            catch (HubException)
            {
                throw; // Re-throw HubException without additional logging
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering playlist {PlaylistId} in session {SessionId}", playlistId, sessionId);
                throw new HubException("Failed to reorder playlist");
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>
        /// Helper method to broadcast playlist updates to all clients in a session group.
        /// </summary>
        private async Task BroadcastPlaylistUpdate(Guid sessionId, Playlist playlist)
        {
            var groupName = GetSessionGroupName(sessionId.ToString());
            var dto = new PlaylistUpdatedDto(
                playlist.Id,
                playlist.SessionId,
                playlist.Items.Select(i => new PlaylistItemDto(
                    i.Id,
                    i.Artist,
                    i.Title,
                    i.SingerName,
                    i.Position
                )).ToList()
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
    public record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position);
    public record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, System.Collections.Generic.List<PlaylistItemDto> Items);
}
