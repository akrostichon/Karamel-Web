using Fluxor;
using Karamel.Web.Models;
using Karamel.Web.Store.Library;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Karamel.Web.Services;

/// <summary>
/// Manages session state synchronization between tabs using Broadcast Channel API
/// and sessionStorage persistence
/// </summary>
public class SessionService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IState<SessionState> _sessionState;
    private readonly IState<LibraryState> _libraryState;
    private readonly IState<PlaylistState> _playlistState;
    private readonly IDispatcher _dispatcher;
    private IJSObjectReference? _sessionBridgeModule;
    private bool _isInitialized;
    private bool _isMainTab;

    public SessionService(
        IJSRuntime jsRuntime,
        IState<SessionState> sessionState,
        IState<LibraryState> libraryState,
        IState<PlaylistState> playlistState,
        IDispatcher dispatcher)
    {
        _jsRuntime = jsRuntime;
        _sessionState = sessionState;
        _libraryState = libraryState;
        _playlistState = playlistState;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Initialize session bridge with JavaScript module
    /// </summary>
    /// <param name="asMainTab">Whether this tab has directory handle (main tab)</param>
    public async Task InitializeAsync(bool asMainTab)
    {
        if (_isInitialized)
            return;

        _isMainTab = asMainTab;
        _sessionBridgeModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/sessionBridge.js");

        await _sessionBridgeModule.InvokeVoidAsync("initializeSession", asMainTab);

        // Load existing session state from sessionStorage
        if (!asMainTab)
        {
            await RestoreSessionStateAsync();
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Broadcast library loaded event (main tab only)
    /// </summary>
    public async Task BroadcastLibraryLoadedAsync(IEnumerable<Song> songs)
    {
        if (!_isMainTab || _sessionBridgeModule == null)
            return;

        var data = new
        {
            songs = songs.Select(s => new
            {
                id = s.Id.ToString(),
                artist = s.Artist,
                title = s.Title,
                mp3FileName = s.Mp3FileName,
                cdgFileName = s.CdgFileName
            }).ToArray()
        };

        await _sessionBridgeModule.InvokeVoidAsync("broadcastStateUpdate", "library-loaded", data);
    }

    /// <summary>
    /// Broadcast playlist updated event (main tab only)
    /// </summary>
    public async Task BroadcastPlaylistUpdatedAsync()
    {
        if (!_isMainTab || _sessionBridgeModule == null)
            return;

        var state = _playlistState.Value;
        var data = new
        {
            queue = state.Queue.Select(s => new
            {
                id = s.Id.ToString(),
                artist = s.Artist,
                title = s.Title,
                addedBySinger = s.AddedBySinger
            }).ToArray(),
            currentSong = state.CurrentSong == null ? null : new
            {
                id = state.CurrentSong.Id.ToString(),
                artist = state.CurrentSong.Artist,
                title = state.CurrentSong.Title,
                addedBySinger = state.CurrentSong.AddedBySinger
            },
            currentSingerName = state.CurrentSingerName,
            singerSongCounts = state.SingerSongCounts
        };

        await _sessionBridgeModule.InvokeVoidAsync("broadcastStateUpdate", "playlist-updated", data);
    }

    /// <summary>
    /// Broadcast session settings (main tab only)
    /// </summary>
    public async Task BroadcastSessionSettingsAsync(Session session)
    {
        if (!_isMainTab || _sessionBridgeModule == null)
            return;

        var data = new
        {
            sessionId = session.SessionId.ToString(),
            createdAt = session.CreatedAt,
            libraryPath = session.LibraryPath,
            requireSingerName = session.RequireSingerName,
            pauseBetweenSongs = session.PauseBetweenSongs,
            pauseBetweenSongsSeconds = session.PauseBetweenSongsSeconds,
            filenamePattern = session.FilenamePattern
        };

        await _sessionBridgeModule.InvokeVoidAsync("broadcastStateUpdate", "session-settings", data);
    }

    /// <summary>
    /// Broadcast current song change (main tab only)
    /// </summary>
    public async Task BroadcastCurrentSongAsync(Song? song, string? singerName)
    {
        if (!_isMainTab || _sessionBridgeModule == null)
            return;

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

        await _sessionBridgeModule.InvokeVoidAsync("broadcastStateUpdate", "current-song", data);
    }

    /// <summary>
    /// Generate session URL with SessionId query parameter
    /// </summary>
    public async Task<string> GenerateSessionUrlAsync(string path, Guid sessionId)
    {
        if (_sessionBridgeModule == null)
            throw new InvalidOperationException("Session bridge not initialized");

        return await _sessionBridgeModule.InvokeAsync<string>(
            "generateSessionUrl", path, sessionId.ToString());
    }

    /// <summary>
    /// Get SessionId from current URL query parameter
    /// </summary>
    public async Task<Guid?> GetSessionIdFromUrlAsync()
    {
        if (_sessionBridgeModule == null)
            throw new InvalidOperationException("Session bridge not initialized");

        var sessionIdString = await _sessionBridgeModule.InvokeAsync<string?>("getSessionIdFromUrl");
        
        return Guid.TryParse(sessionIdString, out var sessionId) ? sessionId : null;
    }

    /// <summary>
    /// Restore session state from sessionStorage (secondary tabs)
    /// </summary>
    private async Task RestoreSessionStateAsync()
    {
        if (_sessionBridgeModule == null)
            return;

        try
        {
            var stateJson = await _sessionBridgeModule.InvokeAsync<JsonElement>("getSessionState");

            // Restore session settings
            if (stateJson.TryGetProperty("session", out var sessionData) && 
                sessionData.ValueKind != JsonValueKind.Null)
            {
                // TODO: Dispatch action to restore session
                // This will be implemented when we have the necessary actions
            }

            // Restore library
            if (stateJson.TryGetProperty("library", out var libraryData) && 
                libraryData.ValueKind != JsonValueKind.Null)
            {
                // TODO: Dispatch action to restore library
                // This will be implemented when we have the necessary actions
            }

            // Restore playlist
            if (stateJson.TryGetProperty("playlist", out var playlistData) && 
                playlistData.ValueKind != JsonValueKind.Null)
            {
                // TODO: Dispatch action to restore playlist
                // This will be implemented when we have the necessary actions
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restore session state: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if main tab is still alive (secondary tabs only)
    /// </summary>
    public async Task<bool> CheckMainTabAliveAsync()
    {
        if (_isMainTab || _sessionBridgeModule == null)
            return true;

        try
        {
            return await _sessionBridgeModule.InvokeAsync<bool>("checkMainTabAlive");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clear session state (when session ends)
    /// </summary>
    public async Task ClearSessionAsync()
    {
        if (_sessionBridgeModule == null)
            return;

        await _sessionBridgeModule.InvokeVoidAsync("clearSessionState");
    }

    public async ValueTask DisposeAsync()
    {
        if (_sessionBridgeModule != null)
        {
            await _sessionBridgeModule.DisposeAsync();
        }
    }
}
