using Karamel.Web.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Karamel.Web.Services;

/// <summary>
/// DEPRECATED: Facade for session management services. Use specific services directly.
/// This interface delegates to ISessionStorageService, ISessionApiClient, ISignalRPlaylistBridge,
/// ISignalRConnectionManager, ISongEnrichmentService, and IPlaylistStateSynchronizer.
/// Will be removed in next major version.
/// </summary>
 [Obsolete("Use specific services directly (ISessionStorageService, ISessionApiClient, ISignalRPlaylistBridge, ISignalRConnectionManager, ISongEnrichmentService, IPlaylistStateSynchronizer). Will be removed in next major version.")]
public interface ISessionService : IAsyncDisposable
{
    /// <summary>
    /// Initialize session bridge with JavaScript module
    /// Use: ISignalRConnectionManager.InitializeAsync + IPlaylistStateSynchronizer.RestoreSessionStateAsync + IPlaylistStateSynchronizer.SetupStateUpdateL istenerAsync
    /// </summary>
    [Obsolete("Use ISignalRConnectionManager.InitializeAsync, IPlaylistStateSynchronizer.RestoreSessionStateAsync, and IPlaylistStateSynchronizer.SetupStateUpdateListenerAsync")]
    Task InitializeAsync(Guid sessionId, bool asMainTab, string? linkToken = null);

    /// <summary>
    /// Use: ISessionApiClient.UploadLibraryToServerAsync
    /// </summary>
    [Obsolete("Use ISessionApiClient.UploadLibraryToServerAsync")]
    Task<bool> UploadLibraryToServerAsync(Guid sessionId, IEnumerable<Song> songs, string? linkToken = null);

    /// <summary>
    /// Fetch a paginated library page from server
    /// Use: ISessionApiClient.FetchLibraryPageAsync
    /// </summary>
    [Obsolete("Use ISessionApiClient.FetchLibraryPageAsync")]
    Task<JsonElement> FetchLibraryPageAsync(Guid sessionId, int page = 1, int pageSize = 50, string? search = null, string? sort = null);

    /// <summary>
    /// Use: ISessionApiClient.SearchLibraryAsync
    /// </summary>
    [Obsolete("Use ISessionApiClient.SearchLibraryAsync")]
    Task<JsonElement> SearchLibraryAsync(Guid sessionId, string query, int maxResults = 10);

    /// <summary>
    /// Broadcast playlist updated event (main tab only)
    /// Use: ISignalRPlaylistBridge.BroadcastPlaylistUpdatedAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.BroadcastPlaylistUpdatedAsync")]
    Task BroadcastPlaylistUpdatedAsync();

    /// <summary>
    /// Broadcast session settings (main tab only)
    /// Use: ISignalRPlaylistBridge.BroadcastSessionSettingsAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.BroadcastSessionSettingsAsync")]
    Task BroadcastSessionSettingsAsync(Session session);

    /// <summary>
    /// Broadcast current song change (main tab only)
    /// Use: ISignalRPlaylistBridge.BroadcastCurrentSongAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.BroadcastCurrentSongAsync")]
    Task BroadcastCurrentSongAsync(Song? song, string? singerName);

    /// <summary>
    /// Generate session URL with SessionId and LinkToken query parameters
    /// Use: ISessionStorageService.GenerateSessionUrlAsync
    /// </summary>
    [Obsolete("Use ISessionStorageService.GenerateSessionUrlAsync")]
    Task<string> GenerateSessionUrlAsync(string path, Guid sessionId, string? linkToken = null);

    /// <summary>
    /// Get SessionId from current URL query parameter
    /// Use: ISessionStorageService.GetSessionIdFromUrlAsync
    /// </summary>
    [Obsolete("Use ISessionStorageService.GetSessionIdFromUrlAsync")]
    Task<Guid?> GetSessionIdFromUrlAsync();

    /// <summary>
    /// Check if main tab is still alive (secondary tabs only)
    /// Use: ISignalRConnectionManager.CheckMainTabAliveAsync
    /// </summary>
    [Obsolete("Use ISignalRConnectionManager.CheckMainTabAliveAsync")]
    Task<bool> CheckMainTabAliveAsync();

    /// <summary>
    /// Gets whether this tab is the main tab (has directory handle)
    /// Use: ISignalRConnectionManager.IsMainTab
    /// </summary>
    [Obsolete("Use ISignalRConnectionManager.IsMainTab")]
    bool IsMainTab { get; }

    /// <summary>
    /// Clear session state (when session ends)
    /// Use: ISessionStorageService.ClearSessionAsync
    /// </summary>
    [Obsolete("Use ISessionStorageService.ClearSessionAsync")]
    Task ClearSessionAsync();

    /// <summary>
    /// Add an item to the playlist via SignalR if available, fallback to broadcast
    /// Use: ISignalRPlaylistBridge.AddItemToPlaylistAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.AddItemToPlaylistAsync")]
    Task<bool> AddItemToPlaylistAsync(Song song);

    /// <summary>
    /// Remove an item from the playlist via SignalR if available, fallback to broadcast
    /// Use: ISignalRPlaylistBridge.RemoveItemFromPlaylistAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.RemoveItemFromPlaylistAsync")]
    Task<bool> RemoveItemFromPlaylistAsync(Guid itemId);

    /// <summary>
    /// Reorder the playlist via SignalR
    /// Use: ISignalRPlaylistBridge.ReorderPlaylistAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.ReorderPlaylistAsync")]
    Task<bool> ReorderPlaylistAsync(int from, int to);

    /// <summary>
    /// Set song status via SignalR
    /// Use: ISignalRPlaylistBridge.SetSongStatusAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.SetSongStatusAsync")]
    Task SetSongStatusAsync(string itemId, int status);

    /// <summary>
    /// Advance to next song via SignalR
    /// Use: ISignalRPlaylistBridge.AdvanceToNextSongAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.AdvanceToNextSongAsync")]
    Task AdvanceToNextSongAsync();

    /// <summary>
    /// Complete current song without advancing to next song via SignalR
    /// Use: ISignalRPlaylistBridge.CompleteCurrentSongAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.CompleteCurrentSongAsync")]
    Task CompleteCurrentSongAsync();

    /// <summary>
    /// Clear all queued and up-next songs via SignalR, preserving the currently playing song
    /// Use: ISignalRPlaylistBridge.ClearQueueAsync
    /// </summary>
    [Obsolete("Use ISignalRPlaylistBridge.ClearQueueAsync")]
    Task ClearQueueAsync();

    /// <summary>
    /// Handle state update from broadcast (called by JavaScript via JSInvokable)
    /// Use: IPlaylistStateSynchronizer.OnStateUpdated
    /// </summary>
    [Obsolete("Use IPlaylistStateSynchronizer.OnStateUpdated")]
    [JSInvokable]
    void OnStateUpdated(string type, JsonElement data);
}
