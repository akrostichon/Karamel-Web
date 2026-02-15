using Microsoft.JSInterop;
using Karamel.Web.Models;

namespace Karamel.Web.Services;

/// <summary>
/// Service wrapping PlaylistHub method calls (8 playlist mutations) + broadcast methods
/// Thin wrapper, returns bool for success/failure, no action dispatching
/// </summary>
public class SignalRPlaylistBridge : ISignalRPlaylistBridge
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ISignalRConnectionManager _connectionManager;
    private readonly ILogger<SignalRPlaylistBridge> _logger;

    public SignalRPlaylistBridge(
        IJSRuntime jsRuntime,
        ISignalRConnectionManager connectionManager,
        ILogger<SignalRPlaylistBridge> logger)
    {
        _jsRuntime = jsRuntime;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Add an item to the playlist using SignalR if available, fallback to local broadcast.
    /// Returns true if the server-side RPC was invoked successfully.
    /// </summary>
    public async Task<bool> AddItemToPlaylistAsync(Song song)
    {
        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return false;

        try
        {
            // Pass only song ID - backend will lookup Artist/Title
            return await module.InvokeAsync<bool>("addItemToPlaylist", song.Id.ToString(), song.AddedBySinger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "addItemToPlaylist JS invoke failed");
            return false;
        }
    }

    /// <summary>
    /// Remove an item from the playlist using SignalR if available, fallback to local broadcast.
    /// Returns true if the server-side RPC was invoked successfully.
    /// </summary>
    public async Task<bool> RemoveItemFromPlaylistAsync(Guid itemId)
    {
        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return false;

        try
        {
            return await module.InvokeAsync<bool>("removeItemFromPlaylist", itemId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "removeItemFromPlaylist JS invoke failed");
            return false;
        }
    }

    /// <summary>
    /// Reorder the playlist using SignalR.
    /// </summary>
    public async Task<bool> ReorderPlaylistAsync(int from, int to)
    {
        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return false;

        try
        {
            return await module.InvokeAsync<bool>("reorderPlaylist", from, to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "reorderPlaylist JS invoke failed");
            return false;
        }
    }

    /// <summary>
    /// Set song status via SignalR.
    /// </summary>
    public async Task SetSongStatusAsync(string itemId, int status)
    {
        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return;

        try
        {
            await module.InvokeVoidAsync("setSongStatus", itemId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "setSongStatus JS invoke failed");
        }
    }

    /// <summary>
    /// Advance to next song via SignalR.
    /// </summary>
    public async Task AdvanceToNextSongAsync()
    {
        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return;

        try
        {
            await module.InvokeVoidAsync("advanceToNextSong");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "advanceToNextSong JS invoke failed");
        }
    }

    /// <summary>
    /// Complete current song without advancing to next song via SignalR.
    /// </summary>
    public async Task CompleteCurrentSongAsync()
    {
        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return;

        try
        {
            await module.InvokeVoidAsync("completeCurrentSong");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "completeCurrentSong JS invoke failed");
        }
    }

    /// <summary>
    /// Clear all queued and up-next songs via SignalR, preserving the currently playing song.
    /// </summary>
    public async Task ClearQueueAsync()
    {
        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return;

        try
        {
            await module.InvokeVoidAsync("clearQueue");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "clearQueue JS invoke failed");
        }
    }

    /// <summary>
    /// Broadcast playlist updated event (main tab only)
    /// DEPRECATED: SignalR handles playlist synchronization now
    /// </summary>
    public async Task BroadcastPlaylistUpdatedAsync()
    {
        // No-op: SignalR broadcasts playlist updates automatically
        await Task.CompletedTask;
    }

    /// <summary>
    /// Broadcast session settings (main tab only) - includes theme
    /// </summary>
    public async Task BroadcastSessionSettingsAsync(Session session)
    {
        if (!_connectionManager.IsMainTab) return;

        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return;

        var data = new
        {
            sessionId = session.SessionId.ToString(),
            createdAt = session.CreatedAt,
            requireSingerName = session.RequireSingerName,
            pauseBetweenSongs = session.PauseBetweenSongs,
            pauseBetweenSongsSeconds = session.PauseBetweenSongsSeconds,
            filenamePattern = session.FilenamePattern,
            theme = session.Theme // FIXED: Include theme in broadcast
        };

        await module.InvokeVoidAsync("broadcastStateUpdate", "session-settings", data);
    }

    /// <summary>
    /// Broadcast current song change (main tab only)
    /// </summary>
    public async Task BroadcastCurrentSongAsync(Song? song, string? singerName)
    {
        if (!_connectionManager.IsMainTab) return;

        var module = await _connectionManager.GetModuleAsync();
        if (module == null) return;

        var data = song == null ? null : new
        {
            song = new
            {
                id = song.Id.ToString(),
                artist = song.Artist,
                title = song.Title,
                addedBySinger = song.AddedBySinger
            },
            singerName
        };

        await module.InvokeVoidAsync("broadcastStateUpdate", "current-song", data);
    }
}
