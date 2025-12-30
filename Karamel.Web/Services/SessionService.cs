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
public class SessionService : ISessionService
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
    /// <param name="sessionId">Session GUID</param>
    /// <param name="asMainTab">Whether this tab has directory handle (main tab)</param>
    public async Task InitializeAsync(Guid sessionId, bool asMainTab)
    {
        if (_isInitialized)
            return;

        _isMainTab = asMainTab;
        _sessionBridgeModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/sessionBridge.js");

        await _sessionBridgeModule.InvokeVoidAsync("initializeSession", sessionId.ToString(), asMainTab);

        // Load existing session state from sessionStorage
        if (!asMainTab)
        {
            await RestoreSessionStateAsync(sessionId);
            
            // Set up listener for ongoing state updates from main tab
            await SetupStateUpdateListenerAsync();
        }

        // Ensure state update listener is registered even for main tab so
        // it receives session-state-updated events and can invoke OnStateUpdated.
        await SetupStateUpdateListenerAsync();

        _isInitialized = true;
    }

    /// <summary>
    /// Save library to sessionStorage (main tab only, called once during session initialization)
    /// </summary>
    public async Task SaveLibraryToSessionStorageAsync(Guid sessionId, IEnumerable<Song> songs)
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

        await _sessionBridgeModule.InvokeVoidAsync("saveLibraryToSessionStorage", sessionId.ToString(), data);
    }

    /// <summary>
    /// Broadcast playlist updated event (main tab only)
    /// </summary>
    public async Task BroadcastPlaylistUpdatedAsync()
    {
        if (_sessionBridgeModule == null)
            return;

        var state = _playlistState.Value;
        var data = new
        {
            queue = state.Queue.Select(s => new
            {
                id = s.Id.ToString(),
                artist = s.Artist,
                title = s.Title,
                mp3FileName = s.Mp3FileName,
                cdgFileName = s.CdgFileName,
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
    /// Library is read from sessionStorage - already saved by main tab during session init
    /// </summary>
    private async Task RestoreSessionStateAsync(Guid sessionId)
    {
        if (_sessionBridgeModule == null)
            return;

        try
        {
            Console.WriteLine($"SessionService: Starting to restore session {sessionId}");
            
            // Wait for state sync response (with timeout)
            var syncCompletionSource = new TaskCompletionSource<bool>();
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            
            // Set up event listener for state sync
            var dotNetRef = DotNetObjectReference.Create(new StateSync(syncCompletionSource));
            await _sessionBridgeModule.InvokeVoidAsync("setupStateSyncListener", dotNetRef);
            
            // Wait for sync or timeout
            var syncTask = syncCompletionSource.Task;
            var timeoutTask = Task.Delay(2000, timeoutCts.Token);
            var completedTask = await Task.WhenAny(syncTask, timeoutTask);
            
            if (completedTask == syncTask)
            {
                Console.WriteLine($"SessionService: State sync completed");
            }
            else
            {
                Console.WriteLine($"SessionService: State sync timed out, using current sessionStorage");
            }
            
            // Now read from sessionStorage (which should have been updated by the sync)
            var stateJson = await _sessionBridgeModule.InvokeAsync<JsonElement>("getSessionStateForSession", sessionId.ToString());

            Console.WriteLine($"SessionService: Got state from sessionStorage: {stateJson}");

            // Restore session settings
            if (stateJson.TryGetProperty("session", out var sessionData) && 
                sessionData.ValueKind != JsonValueKind.Null)
            {
                Console.WriteLine($"SessionService: Found session data in sessionStorage");
                var session = new Models.Session
                {
                    SessionId = sessionId,
                    LibraryPath = sessionData.GetProperty("libraryPath").GetString() ?? "",
                    RequireSingerName = sessionData.GetProperty("requireSingerName").GetBoolean(),
                    AllowSingersToReorder = sessionData.TryGetProperty("allowSingerReorder", out var allowReorder) ? allowReorder.GetBoolean() : false,
                    PauseBetweenSongs = sessionData.TryGetProperty("pauseBetweenSongs", out var pauseEnabled) ? pauseEnabled.GetBoolean() : true,
                    PauseBetweenSongsSeconds = sessionData.GetProperty("pauseBetweenSongsSeconds").GetInt32(),
                    FilenamePattern = sessionData.GetProperty("filenamePattern").GetString() ?? "%artist - %title"
                };
                
                Console.WriteLine($"SessionService: Dispatching InitializeSessionAction");
                _dispatcher.Dispatch(new InitializeSessionAction(session));
            }
            else
            {
                Console.WriteLine($"SessionService: No session data found in sessionStorage");
            }

            // Restore library (saved by main tab during init)
            if (stateJson.TryGetProperty("library", out var libraryData) && 
                libraryData.ValueKind != JsonValueKind.Null &&
                libraryData.TryGetProperty("songs", out var songsArray))
            {
                Console.WriteLine($"SessionService: Found library data with {songsArray.GetArrayLength()} songs");
                var songs = songsArray.EnumerateArray().Select(s => new Song
                {
                    Id = Guid.Parse(s.GetProperty("id").GetString()!),
                    Artist = s.GetProperty("artist").GetString() ?? "",
                    Title = s.GetProperty("title").GetString() ?? "",
                    Mp3FileName = s.GetProperty("mp3FileName").GetString() ?? "",
                    CdgFileName = s.GetProperty("cdgFileName").GetString() ?? ""
                }).ToList();
                
                Console.WriteLine($"SessionService: Dispatching LoadLibrarySuccessAction with {songs.Count} songs");
                _dispatcher.Dispatch(new LoadLibrarySuccessAction(songs));
            }
            else
            {
                Console.WriteLine($"SessionService: No library data found in sessionStorage");
            }

            // Note: Playlist state doesn't need to be restored here initially as it starts empty
            // It will be updated via broadcast when songs are added

            // Restore playlist if present in sessionStorage (useful if main tab saved it)
            if (stateJson.TryGetProperty("playlist", out var playlistData) &&
                playlistData.ValueKind != JsonValueKind.Null)
            {
                try
                {
                    Console.WriteLine($"SessionService: Found playlist data in sessionStorage");

                    var queue = new List<Song>();
                    if (playlistData.TryGetProperty("queue", out var queueArray))
                    {
                        queue = queueArray.EnumerateArray().Select(s => new Song
                        {
                            Id = Guid.Parse(s.GetProperty("id").GetString()!),
                            Artist = s.GetProperty("artist").GetString() ?? "",
                            Title = s.GetProperty("title").GetString() ?? "",
                            Mp3FileName = s.GetProperty("mp3FileName").GetString() ?? "",
                            CdgFileName = s.GetProperty("cdgFileName").GetString() ?? "",
                            AddedBySinger = s.TryGetProperty("addedBySinger", out var singer) ? singer.GetString() : null
                        }).ToList();
                    }

                    var singerSongCounts = new Dictionary<string, int>();
                    if (playlistData.TryGetProperty("singerSongCounts", out var countsObj))
                    {
                        foreach (var prop in countsObj.EnumerateObject())
                        {
                            singerSongCounts[prop.Name] = prop.Value.GetInt32();
                        }
                    }

                    _dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(queue, singerSongCounts));
                    Console.WriteLine($"SessionService: Dispatched playlist restore with {queue.Count} songs");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SessionService: Error restoring playlist from sessionStorage: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restore session state: {ex.Message}");
            Console.WriteLine($"Exception details: {ex}");
        }
    }
    
    /// <summary>
    /// Set up listener for ongoing state updates from main tab (secondary tabs only)
    /// </summary>
    private async Task SetupStateUpdateListenerAsync()
    {
        if (_sessionBridgeModule == null)
            return;

        var dotNetRef = DotNetObjectReference.Create(this);
        Console.WriteLine($"SessionService: Registering state update listener (isMainTab={_isMainTab})");
        await _sessionBridgeModule.InvokeVoidAsync("setupStateUpdateListener", dotNetRef);
    }

    /// <summary>
    /// Handle state update from broadcast (called by JavaScript)
    /// </summary>
    [JSInvokable]
    public void OnStateUpdated(string type, JsonElement data)
    {
        try
        {
            Console.WriteLine($"SessionService: Received state update: {type}. PayloadKind={data.ValueKind}");
            
            switch (type)
            {
                case "playlist-updated":
                    HandlePlaylistUpdate(data);
                    break;
                case "session-settings":
                    HandleSessionSettingsUpdate(data);
                    break;
                case "current-song":
                    HandleCurrentSongUpdate(data);
                    break;
                default:
                    Console.WriteLine($"SessionService: Unknown state update type: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: Error handling state update: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle playlist update from broadcast
    /// </summary>
    private void HandlePlaylistUpdate(JsonElement data)
    {
        try
        {
            // Extract queue
            if (data.TryGetProperty("queue", out var queueArray))
            {
                Console.WriteLine($"SessionService: Playlist update contains queue with {queueArray.GetArrayLength()} items");
                var queue = queueArray.EnumerateArray().Select(s => new Song
                {
                    Id = Guid.Parse(s.GetProperty("id").GetString()!),
                    Artist = s.GetProperty("artist").GetString() ?? "",
                    Title = s.GetProperty("title").GetString() ?? "",
                    Mp3FileName = s.GetProperty("mp3FileName").GetString() ?? "",
                    CdgFileName = s.GetProperty("cdgFileName").GetString() ?? "",
                    AddedBySinger = s.TryGetProperty("addedBySinger", out var singer) ? singer.GetString() : null
                }).ToList();

                // Extract singer song counts
                var singerSongCounts = new Dictionary<string, int>();
                if (data.TryGetProperty("singerSongCounts", out var countsObj))
                {
                    foreach (var prop in countsObj.EnumerateObject())
                    {
                        singerSongCounts[prop.Name] = prop.Value.GetInt32();
                    }
                }

                // Log sample of first item for diagnostics
                if (queue.Count > 0)
                {
                    var first = queue[0];
                    Console.WriteLine($"SessionService: First queued song: id={first.Id} artist={first.Artist} title={first.Title} addedBy={first.AddedBySinger}");
                }

                // Dispatch action to update playlist state
                _dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(queue, singerSongCounts));

                Console.WriteLine($"SessionService: Dispatched playlist update with {queue.Count} songs");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: Error parsing playlist update: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle session settings update from broadcast
    /// </summary>
    private void HandleSessionSettingsUpdate(JsonElement data)
    {
        // Not currently needed for this issue, but included for completeness
        Console.WriteLine($"SessionService: Session settings update received");
    }

    /// <summary>
    /// Handle current song update from broadcast
    /// </summary>
    private void HandleCurrentSongUpdate(JsonElement data)
    {
        // Not currently needed for this issue, but included for completeness
        Console.WriteLine($"SessionService: Current song update received");
    }
    
    private class StateSync
    {
        private readonly TaskCompletionSource<bool> _completionSource;
        
        public StateSync(TaskCompletionSource<bool> completionSource)
        {
            _completionSource = completionSource;
        }
        
        [JSInvokable]
        public void OnStateSynced()
        {
            _completionSource.TrySetResult(true);
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
