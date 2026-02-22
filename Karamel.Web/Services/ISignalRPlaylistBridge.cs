using Karamel.Web.Models;

namespace Karamel.Web.Services;

/// <summary>
/// Service wrapping PlaylistHub method calls (8 playlist mutations) + broadcast methods
/// </summary>
public interface ISignalRPlaylistBridge
{
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
    /// Complete current song without advancing to next song via SignalR.
    /// </summary>
    Task CompleteCurrentSongAsync();

    /// <summary>
    /// Clear all queued and up-next songs via SignalR, preserving the currently playing song.
    /// </summary>
    Task ClearQueueAsync();

    /// <summary>
    /// Broadcast playlist updated event (main tab only) - FALLBACK for BroadcastChannel
    /// DEPRECATED: SignalR handles playlist synchronization now
    /// </summary>
    Task BroadcastPlaylistUpdatedAsync();

    /// <summary>
    /// Pause the session via SignalR hub (admin only).
    /// Hub broadcasts ReceiveSessionPaused to all clients.
    /// </summary>
    Task PauseSessionAsync();

    /// <summary>
    /// Resume the session via SignalR hub (admin only).
    /// Hub broadcasts ReceiveSessionResumed to all clients.
    /// </summary>
    Task ResumeSessionAsync();

    /// <summary>
    /// Broadcast session settings (main tab only) - includes theme
    /// </summary>
    Task BroadcastSessionSettingsAsync(Session session);

    /// <summary>
    /// Broadcast current song change (main tab only)
    /// </summary>
    Task BroadcastCurrentSongAsync(Song? song, string? singerName);
}
