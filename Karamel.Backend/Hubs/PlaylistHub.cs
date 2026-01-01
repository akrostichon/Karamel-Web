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

        // Per-session semaphores to serialize mutations and avoid races.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, System.Threading.SemaphoreSlim> _sessionLocks
            = new();

        private static System.Threading.SemaphoreSlim GetSessionLock(Guid sessionId) =>
            _sessionLocks.GetOrAdd(sessionId, _ => new System.Threading.SemaphoreSlim(1, 1));

        public PlaylistHub(IPlaylistRepository playlistRepo, ISessionRepository sessionRepo)
        {
            _playlistRepo = playlistRepo;
            _sessionRepo = sessionRepo;
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
        public async Task AddItemAsync(Guid sessionId, Guid playlistId, string artist, string title, string? singerName)
        {
            var sem = GetSessionLock(sessionId);
            await sem.WaitAsync();
            try
            {
                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null)
                {
                    throw new HubException("Session not found");
                }

                var playlist = await _playlistRepo.GetAsync(playlistId);
                if (playlist == null || playlist.SessionId != sessionId)
                {
                    throw new HubException("Playlist not found or does not belong to session");
                }

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

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
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
                var playlist = await _playlistRepo.GetAsync(playlistId);
                if (playlist == null || playlist.SessionId != sessionId)
                {
                    throw new HubException("Playlist not found or does not belong to session");
                }

                var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
                if (item == null)
                {
                    throw new HubException("Item not found in playlist");
                }

                playlist.Items.Remove(item);

                // Re-index positions
                for (int i = 0; i < playlist.Items.Count; i++)
                {
                    playlist.Items[i].Position = i;
                }

                await _playlistRepo.UpdateAsync(playlist);

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
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
                var playlist = await _playlistRepo.GetAsync(playlistId);
                if (playlist == null || playlist.SessionId != sessionId)
                {
                    throw new HubException("Playlist not found or does not belong to session");
                }

                if (from < 0 || from >= playlist.Items.Count || to < 0 || to >= playlist.Items.Count)
                {
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

                // Broadcast update to all clients in the session group
                await BroadcastPlaylistUpdate(sessionId, playlist);
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
    }

    // DTOs for hub payloads (shared with controller for now)
    public record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position);
    public record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, System.Collections.Generic.List<PlaylistItemDto> Items);
}
