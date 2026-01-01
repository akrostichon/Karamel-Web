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
    Task InitializeAsync(Guid sessionId, bool asMainTab);

    /// <summary>
    /// Save library to sessionStorage (main tab only, called once during session initialization)
    /// </summary>
    Task SaveLibraryToSessionStorageAsync(Guid sessionId, IEnumerable<Song> songs);

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
    /// Generate session URL with SessionId query parameter
    /// </summary>
    Task<string> GenerateSessionUrlAsync(string path, Guid sessionId);

    /// <summary>
    /// Get SessionId from current URL query parameter
    /// </summary>
    Task<Guid?> GetSessionIdFromUrlAsync();

    /// <summary>
    /// Check if main tab is still alive (secondary tabs only)
    /// </summary>
    Task<bool> CheckMainTabAliveAsync();

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
    /// Reorder the playlist via SignalR if available, fallback to broadcast.
    /// Returns true if server RPC was invoked.
    /// </summary>
    Task<bool> ReorderPlaylistAsync(IEnumerable<Song> newOrder);

    /// <summary>
    /// Handle state update from broadcast (called by JavaScript)
    /// </summary>
    [JSInvokable]
    void OnStateUpdated(string type, JsonElement data);
}
