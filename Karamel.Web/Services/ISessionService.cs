using Karamel.Web.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Karamel.Web.Services;

/// <summary>
/// Interface for managing session state synchronization between tabs using Broadcast Channel API
/// and sessionStorage persistence
/// </summary>
public interface ISessionService : IAsyncDisposable
{
    /// <summary>
    /// Initialize session bridge with JavaScript module
    /// </summary>
    /// <param name="sessionId">Session GUID</param>
    /// <param name="asMainTab">Whether this tab has directory handle (main tab)</param>
    Task InitializeAsync(Guid sessionId, bool asMainTab, string? linkToken = null);

    Task<bool> UploadLibraryToServerAsync(Guid sessionId, IEnumerable<Song> songs, string? linkToken = null);

    /// <summary>
    /// Fetch a paginated library page from server (prefers SignalR RPC when available)
    /// Returns a JSON element containing { items, page, pageSize, totalCount }
    /// </summary>
    Task<System.Text.Json.JsonElement> FetchLibraryPageAsync(Guid sessionId, int page = 1, int pageSize = 50, string? search = null, string? sort = null);
    Task<System.Text.Json.JsonElement> SearchLibraryAsync(Guid sessionId, string query, int maxResults = 10);

    /// <summary>
    /// Broadcast playlist updated event (main tab only)
    /// </summary>
    Task BroadcastPlaylistUpdatedAsync();

    /// <summary>
    /// Broadcast session settings (main tab only)
    /// </summary>
    Task BroadcastSessionSettingsAsync(Session session);

    /// <summary>
    /// Broadcast current song change (main tab only)
    /// </summary>
    Task BroadcastCurrentSongAsync(Song? song, string? singerName);

    /// <summary>
    /// Generate session URL with SessionId and LinkToken query parameters
    /// </summary>
    Task<string> GenerateSessionUrlAsync(string path, Guid sessionId, string? linkToken = null);

    /// <summary>
    /// Get SessionId from current URL query parameter
    /// </summary>
    Task<Guid?> GetSessionIdFromUrlAsync();

    /// <summary>
    /// Check if main tab is still alive (secondary tabs only)
    /// </summary>
    Task<bool> CheckMainTabAliveAsync();

    /// <summary>
    /// Gets whether this tab is the main tab (has directory handle)
    /// </summary>
    bool IsMainTab { get; }

    /// <summary>
    /// Clear session state (when session ends)
    /// </summary>
    Task ClearSessionAsync();

    /// <summary>
    /// Add an item to the playlist via SignalR if available, fallback to broadcast.
    /// Returns true if server RPC was invoked.
    /// </summary>
    Task<bool> AddItemToPlaylistAsync(Song song);

    /// <summary>
    /// Remove an item from the playlist via SignalR if available, fallback to broadcast.
    /// Returns true if server RPC was invoked.
    /// </summary>
    Task<bool> RemoveItemFromPlaylistAsync(Guid itemId);

    /// <summary>
    /// Reorder the playlist via SignalR.
    /// </summary>
    Task<bool> ReorderPlaylistAsync(int from, int to);

    /// <summary>
    /// Set song status via SignalR.
    /// </summary>
    Task SetSongStatusAsync(string itemId, int status);

    /// <summary>
    /// Advance to next song via SignalR.
    /// </summary>
    Task AdvanceToNextSongAsync();

    /// <summary>
    /// Clear all queued and up-next songs via SignalR, preserving the currently playing song.
    /// </summary>
    Task ClearQueueAsync();

    /// <summary>
    /// Handle state update from broadcast (called by JavaScript)
    /// </summary>
    [JSInvokable]
    void OnStateUpdated(string type, JsonElement data);
}
