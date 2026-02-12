using Fluxor;
using Karamel.Web.Models;
using Karamel.Web.Store.Library;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;
using Karamel.Web.Contracts;

namespace Karamel.Web.Services;

/// <summary>
/// Manages session state synchronization between tabs using Broadcast Channel API
/// and sessionStorage persistence
/// </summary>
public class SessionService : ISessionService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IState<LibraryState> _libraryState;
    private readonly IDispatcher _dispatcher;
    private readonly HttpClient _httpClient;
    private IJSObjectReference? _sessionBridgeModule;
    private bool _isInitialized;
    private bool _isMainTab;
    private DotNetObjectReference<SessionService>? _stateUpdateDotNetRef;

    /// <summary>
    /// Gets whether this tab is the main tab (has directory handle)
    /// </summary>
    public bool IsMainTab => _isMainTab;

    // Song (de)serialization helpers moved to Karamel.Web.Contracts.SongDto / SongConverters

    public SessionService(
        IJSRuntime jsRuntime,
        IState<LibraryState> libraryState,
        IDispatcher dispatcher,
        HttpClient httpClient)
    {
        _jsRuntime = jsRuntime;
        _libraryState = libraryState;
        _dispatcher = dispatcher;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Initialize session bridge with JavaScript module
    /// </summary>
    /// <param name="sessionId">Session GUID</param>
    /// <param name="asMainTab">Whether this tab has directory handle (main tab)</param>
    public async Task InitializeAsync(Guid sessionId, bool asMainTab, string? linkToken = null)
    {
        if (_isInitialized)
            return;

        _isMainTab = asMainTab;
        _sessionBridgeModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/signalRBridge.js");

        // Get backend base address for SignalR connection
        var backendBase = _httpClient.BaseAddress?.ToString().TrimEnd('/');

        // Pass link token and backend URL if present so JS SignalR client can use them when connecting
        await _sessionBridgeModule.InvokeVoidAsync("initializeSession", sessionId.ToString(), asMainTab, linkToken, backendBase);

        // Load existing session state from sessionStorage
        if (!asMainTab)
        {
            await RestoreSessionStateAsync(sessionId);
        }

        // Ensure state update listener is registered for all tabs so
        // they receive session-state-updated events and can invoke OnStateUpdated.
        await SetupStateUpdateListenerAsync();

        _isInitialized = true;
    }

    /// <summary>
    /// Upload sanitized library to server-side API for paginated listing (main tab only)
    /// PRIVACY: Uses ConvertSongToUploadDto which excludes file paths
    /// </summary>
    public async Task<bool> UploadLibraryToServerAsync(Guid sessionId, IEnumerable<Song> songs, string? linkToken = null)
    {
        if (!_isMainTab || _sessionBridgeModule == null)
            return false;

        var data = new
        {
            songs = songs.Select(SongConverters.ConvertSongToUploadDto).ToArray()  // PRIVACY: Use sanitized DTO
        };

        try
        {
            return await _sessionBridgeModule.InvokeAsync<bool>("uploadLibraryToServer", sessionId.ToString(), data, new { linkToken });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: uploadLibraryTo Server failed: {ex.Message}");
            return false;
        }
    }

    public async Task<JsonElement> FetchLibraryPageAsync(Guid sessionId, int page = 1, int pageSize = 50, string? search = null, string? sort = null)
    {
        if (_sessionBridgeModule == null)
            return default;

        try
        {
            var result = await _sessionBridgeModule.InvokeAsync<JsonElement>("fetchLibraryPage", sessionId.ToString(), page, pageSize, search, sort);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: FetchLibraryPageAsync failed: {ex.Message}");
            return default;
        }
    }

    public async Task<JsonElement> SearchLibraryAsync(Guid sessionId, string query, int maxResults = 10)
    {
        if (_sessionBridgeModule == null)
            return default;

        try
        {
            var result = await _sessionBridgeModule.InvokeAsync<JsonElement>("searchLibrary", sessionId.ToString(), query, maxResults);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: SearchLibraryAsync failed: {ex.Message}");
            return default;
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
    /// Generate session URL with SessionId and LinkToken query parameters
    /// </summary>
    public async Task<string> GenerateSessionUrlAsync(string path, Guid sessionId, string? linkToken = null)
    {
        if (_sessionBridgeModule == null)
            throw new InvalidOperationException("Session bridge not initialized");

        return await _sessionBridgeModule.InvokeAsync<string>(
            "generateSessionUrl", path, sessionId.ToString(), linkToken);
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

        DotNetObjectReference<StateSync>? dotNetRef = null;
        try
        {
#if DEBUG
            Console.WriteLine($"SessionService: Starting to restore session {sessionId}");
#endif
            
            dotNetRef = await WaitForStateSyncAsync();
            var stateJson = await ReadSessionStorageAsync(sessionId);
            
            await RestoreSessionConfigAsync(sessionId, stateJson);
            await RestorePlaylistStateAsync(stateJson);
        }
        catch (InvalidOperationException)
        {
            // Re-throw session-specific exceptions (session expired, network errors, etc.)
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restore session state: {ex.Message}");
#if DEBUG
            Console.WriteLine($"Exception details: {ex}");
#endif
        }
        finally
        {
            try
            {
                dotNetRef?.Dispose();
            }
            catch { }
        }
    }

    /// <summary>
    /// Wait for state synchronization from main tab (with timeout)
    /// </summary>
    private async Task<DotNetObjectReference<StateSync>> WaitForStateSyncAsync()
    {
        var syncCompletionSource = new TaskCompletionSource<bool>();
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        // Set up event listener for state sync
        var dotNetRef = DotNetObjectReference.Create(new StateSync(syncCompletionSource));
        await _sessionBridgeModule!.InvokeVoidAsync("setupStateSyncListener", dotNetRef);
        
        // Wait for sync or timeout
        var syncTask = syncCompletionSource.Task;
        var timeoutTask = Task.Delay(5000, timeoutCts.Token);
        var completedTask = await Task.WhenAny(syncTask, timeoutTask);
        
        if (completedTask == syncTask)
        {
#if DEBUG
            Console.WriteLine($"SessionService: State sync completed");
#endif
        }
        else
        {
#if DEBUG
            Console.WriteLine($"SessionService: State sync timed out, using current sessionStorage");
#endif
        }

        return dotNetRef;
    }

    /// <summary>
    /// Read session state from browser sessionStorage
    /// </summary>
    private async Task<JsonElement> ReadSessionStorageAsync(Guid sessionId)
    {
        var stateJson = await _sessionBridgeModule!.InvokeAsync<JsonElement>("getSessionStateForSession", sessionId.ToString());
#if DEBUG
        Console.WriteLine($"SessionService: Got state from sessionStorage: {stateJson}");
#endif
        return stateJson;
    }

    /// <summary>
    /// Restore session configuration from sessionStorage or backend API
    /// </summary>
    private async Task RestoreSessionConfigAsync(Guid sessionId, JsonElement stateJson)
    {
        Console.WriteLine($"[DIAG] SessionService.RestoreSessionConfigAsync: START for sessionId={sessionId}");
        Console.WriteLine($"[DIAG] SessionService: stateJson.ValueKind={stateJson.ValueKind}");
        
        if (stateJson.TryGetProperty("session", out var sessionData) && 
            sessionData.ValueKind != JsonValueKind.Null)
        {
            Console.WriteLine($"[DIAG] SessionService: Found session data in sessionStorage");
            Console.WriteLine($"[DIAG] SessionService: sessionData={sessionData}");
            
            var session = ParseSessionFromJson(sessionId, sessionData);
            
            Console.WriteLine($"[DIAG] SessionService: Parsed session - SessionId={session.SessionId}, RequireSingerName={session.RequireSingerName}");
            Console.WriteLine($"[DIAG] SessionService: Dispatching InitializeSessionAction FROM SESSIONSTORAGE");
            _dispatcher.Dispatch(new InitializeSessionAction(session));
            Console.WriteLine($"[DIAG] SessionService: InitializeSessionAction dispatched successfully");
        }
        else
        {
            Console.WriteLine($"[DIAG] SessionService: No session data found in sessionStorage - MULTI-DEVICE scenario");
            Console.WriteLine($"[DIAG] SessionService: Will fetch from backend API...");
            await FetchSessionConfigFromBackendAsync(sessionId);
        }
        
        Console.WriteLine($"[DIAG] SessionService.RestoreSessionConfigAsync: END");
    }

    /// <summary>
    /// Parse session configuration from JSON
    /// </summary>
    private Session ParseSessionFromJson(Guid sessionId, JsonElement sessionData)
    {
        return new Session
        {
            SessionId = sessionId,
            RequireSingerName = sessionData.GetProperty("requireSingerName").GetBoolean(),
            AllowSingersToReorder = sessionData.TryGetProperty("allowSingerReorder", out var allowReorder) 
                ? allowReorder.GetBoolean() 
                : false,
            PauseBetweenSongs = sessionData.TryGetProperty("pauseBetweenSongs", out var pauseEnabled) 
                ? pauseEnabled.GetBoolean() 
                : true,
            PauseBetweenSongsSeconds = sessionData.GetProperty("pauseBetweenSongsSeconds").GetInt32(),
            FilenamePattern = sessionData.GetProperty("filenamePattern").GetString() ?? "%artist - %title"
        };
    }

    /// <summary>
    /// Fetch session configuration from backend API (multi-device scenario)
    /// </summary>
    private async Task FetchSessionConfigFromBackendAsync(Guid sessionId)
    {
        try
        {
            Console.WriteLine($"[DIAG] SessionService.FetchSessionConfigFromBackendAsync: START for sessionId={sessionId}");
            Console.WriteLine($"[DIAG] SessionService: Making HTTP GET to /api/sessions/{sessionId}");
            
            var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}");
            
            Console.WriteLine($"[DIAG] SessionService: Response StatusCode={response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DIAG] SessionService: Response body={responseBody}");
                
                var sessionDto = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                var session = new Session
                {
                    SessionId = sessionId,
                    RequireSingerName = sessionDto.GetProperty("requireSingerName").GetBoolean(),
                    AllowSingersToReorder = sessionDto.TryGetProperty("allowSingersToReorder", out var allowReorder) 
                        ? allowReorder.GetBoolean() 
                        : false,
                    PauseBetweenSongs = sessionDto.TryGetProperty("pauseBetweenSongs", out var pauseEnabled) 
                        ? pauseEnabled.GetBoolean() 
                        : true,
                    PauseBetweenSongsSeconds = sessionDto.GetProperty("pauseBetweenSongsSeconds").GetInt32(),
                    FilenamePattern = "%artist - %title"
                };
                
                Console.WriteLine($"[DIAG] SessionService: Parsed session from backend - SessionId={session.SessionId}");
                Console.WriteLine($"[DIAG] SessionService: Dispatching InitializeSessionAction FROM BACKEND");
                _dispatcher.Dispatch(new InitializeSessionAction(session));
                Console.WriteLine($"[DIAG] SessionService: InitializeSessionAction dispatched successfully");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"[ERROR] SessionService: Session {sessionId} NOT FOUND on backend (404)");
                throw new InvalidOperationException("Session has expired or does not exist. Please start a new session.");
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ERROR] SessionService: Failed to fetch session - StatusCode={response.StatusCode}, Body={errorBody}");
                throw new InvalidOperationException($"Failed to retrieve session configuration: {response.StatusCode}");
            }
        }
        catch (InvalidOperationException)
        {
            // Re-throw our own exceptions
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] SessionService: Exception in FetchSessionConfigFromBackendAsync: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[ERROR] SessionService: Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException("Unable to connect to session. Please check your network connection and try again.", ex);
        }
    }

    /// <summary>
    /// Restore playlist state from sessionStorage if available
    /// </summary>
    private async Task RestorePlaylistStateAsync(JsonElement stateJson)
    {
        if (stateJson.TryGetProperty("playlist", out var playlistData) &&
            playlistData.ValueKind != JsonValueKind.Null)
        {
#if DEBUG
            Console.WriteLine($"SessionService: Found playlist data in sessionStorage - initializing state");
#endif
            try
            {
                HandlePlaylistUpdate(playlistData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SessionService: Failed to restore playlist from sessionStorage: {ex.Message}");
            }
        }
        else
        {
#if DEBUG
            Console.WriteLine($"SessionService: No playlist data in sessionStorage - will receive initial state from SignalR");
#endif
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Set up listener for ongoing state updates from main tab (secondary tabs only)
    /// </summary>
    private async Task SetupStateUpdateListenerAsync()
    {
        if (_sessionBridgeModule == null)
            return;

        if (_stateUpdateDotNetRef == null)
        {
            _stateUpdateDotNetRef = DotNetObjectReference.Create(this);
        }

#if DEBUG
        Console.WriteLine($"SessionService: Registering state update listener (isMainTab={_isMainTab})");
#endif
        await _sessionBridgeModule.InvokeVoidAsync("setupStateUpdateListener", _stateUpdateDotNetRef);
    }

    /// <summary>
    /// Handle state update from broadcast (called by JavaScript)
    /// </summary>
    [JSInvokable]
    public void OnStateUpdated(string type, JsonElement data)
    {
        try
        {
#if DEBUG
            Console.WriteLine($"SessionService: Received state update: {type}. PayloadKind={data.ValueKind}");
#endif
            
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
    /// Extract and convert queue array from JSON to List of Songs
    /// </summary>
    private List<Song> ExtractQueueFromJson(JsonElement queueArray)
    {
        return queueArray.EnumerateArray().Select(SongConverters.ConvertJsonToSong).ToList();
    }

    /// <summary>
    /// Build library lookup dictionary by song ID for O(1) access
    /// </summary>
    private Dictionary<Guid, Song> BuildLibraryLookup()
    {
        return _libraryState.Value.Songs.ToDictionary(s => s.Id);
    }

    /// <summary>
    /// Enrich songs in the list with file information from library lookup
    /// </summary>
    private void EnrichSongsWithLibraryFiles(List<Song> songs, Dictionary<Guid, Song> libraryLookup)
    {
        for (int i = 0; i < songs.Count; i++)
        {
            var song = songs[i];
            
            // Skip if already has file information
            if (!string.IsNullOrEmpty(song.Mp3FileName) && !string.IsNullOrEmpty(song.CdgFileName))
                continue;
            
            // Look up in local library by ID
            if (libraryLookup.TryGetValue(song.Id, out var libraryMatch))
            {
                // Replace with enriched song (preserving AddedBySinger from playlist)
                songs[i] = libraryMatch with { AddedBySinger = song.AddedBySinger };
#if DEBUG
                Console.WriteLine($"SessionService: Enriched '{song.Artist} - {song.Title}' (ID: {song.Id}) with files: {libraryMatch.Mp3FileName}");
#endif
            }
            else
            {
                Console.WriteLine($"SessionService: WARNING - Could not find song ID {song.Id} ('{song.Artist} - {song.Title}') in local library");
            }
        }
    }

    /// <summary>
    /// Extract singer song counts dictionary from JSON
    /// </summary>
    private Dictionary<string, int> ExtractSingerSongCounts(JsonElement data)
    {
        var singerSongCounts = new Dictionary<string, int>();
        if (data.TryGetProperty("singerSongCounts", out var countsObj))
        {
            foreach (var prop in countsObj.EnumerateObject())
            {
                singerSongCounts[prop.Name] = prop.Value.GetInt32();
            }
        }
        return singerSongCounts;
    }

    /// <summary>
    /// Extract current singer name from JSON
    /// </summary>
    private string? ExtractCurrentSingerName(JsonElement data)
    {
        if (data.TryGetProperty("currentSingerName", out var currentSingerProp) && 
            currentSingerProp.ValueKind != JsonValueKind.Null)
        {
            return currentSingerProp.GetString();
        }
        return null;
    }

    /// <summary>
    /// Extract and optionally enrich current song from JSON
    /// </summary>
    private Song? ExtractCurrentSongFromJson(JsonElement data, Dictionary<Guid, Song>? libraryLookup)
    {
        try
        {
            if (data.TryGetProperty("currentSong", out var currentSongObj) && 
                currentSongObj.ValueKind != JsonValueKind.Null)
            {
                var currentSong = SongConverters.ConvertJsonToSong(currentSongObj);
                
                // Enrich currentSong if needed and we have a library lookup
                if (_isMainTab && currentSong != null && libraryLookup != null &&
                    (string.IsNullOrEmpty(currentSong.Mp3FileName) || string.IsNullOrEmpty(currentSong.CdgFileName)))
                {
                    if (libraryLookup.TryGetValue(currentSong.Id, out var libraryMatch))
                    {
                        currentSong = libraryMatch with { AddedBySinger = currentSong.AddedBySinger };
#if DEBUG
                        Console.WriteLine($"SessionService: Enriched currentSong '{currentSong.Artist} - {currentSong.Title}'");
#endif
                    }
                }
                
                return currentSong;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: Error parsing currentSong: {ex.Message}");
        }
        
        return null;
    }

    /// <summary>
    /// Log first queue item for diagnostics
    /// </summary>
    private void LogFirstQueueItem(List<Song> queue)
    {
#if DEBUG
        if (queue.Count > 0)
        {
            var first = queue[0];
            Console.WriteLine($"SessionService: First queued song: id={first.Id} artist={first.Artist} title={first.Title} addedBy={first.AddedBySinger}");
        }
#endif
    }

    /// <summary>
    /// Handle playlist update from broadcast
    /// </summary>
    private void HandlePlaylistUpdate(JsonElement data)
    {
        try
        {
            // Extract queue
            if (!data.TryGetProperty("queue", out var queueArray))
                return;

#if DEBUG
            Console.WriteLine($"SessionService: Playlist update contains queue with {queueArray.GetArrayLength()} items");
#endif

            // 1. Extract and convert queue
            var queue = ExtractQueueFromJson(queueArray);

            // 2. Enrich songs if main tab
            Dictionary<Guid, Song>? libraryLookup = null;
            if (_isMainTab && _libraryState.Value.Songs.Count > 0)
            {
#if DEBUG
                Console.WriteLine($"SessionService: Enriching {queue.Count} songs with file information from local library ({_libraryState.Value.Songs.Count} songs available)");
#endif
                libraryLookup = BuildLibraryLookup();
                EnrichSongsWithLibraryFiles(queue, libraryLookup);
            }

            // 3. Extract singer song counts
            var singerSongCounts = ExtractSingerSongCounts(data);

            // 4. Log first item for diagnostics
            LogFirstQueueItem(queue);

            // 5. Extract current song and singer name
            var currentSong = ExtractCurrentSongFromJson(data, libraryLookup);
            var currentSingerName = ExtractCurrentSingerName(data);

            // 6. Convert to PlaylistItemDto format and dispatch
            var itemDtos = queue.Select((s, index) => new PlaylistItemDto(
                Id: Guid.NewGuid().ToString(), // Temporary ID for local processing
                SongId: s.Id.ToString(),
                Artist: s.Artist,
                Title: s.Title,
                SingerName: s.AddedBySinger,
                Position: index,
                Status: (int)SongStatus.Queued
            )).ToList();

            var currentSongDto = currentSong != null ? new PlaylistItemDto(
                Id: Guid.NewGuid().ToString(),
                SongId: currentSong.Id.ToString(),
                Artist: currentSong.Artist,
                Title: currentSong.Title,
                SingerName: currentSong.AddedBySinger,
                Position: 0,
                Status: (int)SongStatus.NowPlaying
            ) : null;

            _dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(itemDtos, currentSongDto));
            Console.WriteLine($"SessionService: Dispatched UpdatePlaylistFromBroadcastAction with {queue.Count} songs");
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
#if DEBUG
        // Not currently needed for this issue, but included for completeness
        Console.WriteLine($"SessionService: Session settings update received");
#endif
    }

    /// <summary>
    /// Handle current song update from broadcast
    /// </summary>
    private void HandleCurrentSongUpdate(JsonElement data)
    {
#if DEBUG
        // Not currently needed for this issue, but included for completeness
        Console.WriteLine($"SessionService: Current song update received");
#endif
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

    /// <summary>
    /// Add an item to the playlist using SignalR if available, fallback to local broadcast.
    /// Returns true if the server-side RPC was invoked successfully.
    /// </summary>
    public async Task<bool> AddItemToPlaylistAsync(Song song)
    {
        if (_sessionBridgeModule == null) return false;

        try
        {
            // Pass only song ID - backend will lookup Artist/Title
            return await _sessionBridgeModule.InvokeAsync<bool>("addItemToPlaylist", song.Id.ToString(), song.AddedBySinger);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: addItemToPlaylist JS invoke failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Remove an item from the playlist using SignalR if available, fallback to local broadcast.
    /// Returns true if the server-side RPC was invoked successfully.
    /// </summary>
    public async Task<bool> RemoveItemFromPlaylistAsync(Guid itemId)
    {
        if (_sessionBridgeModule == null) return false;

        try
        {
            return await _sessionBridgeModule.InvokeAsync<bool>("removeItemFromPlaylist", itemId.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: removeItemFromPlaylist JS invoke failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reorder the playlist using SignalR.
    /// </summary>
    public async Task<bool> ReorderPlaylistAsync(int from, int to)
    {
        if (_sessionBridgeModule == null) return false;

        try
        {
            return await _sessionBridgeModule.InvokeAsync<bool>("reorderPlaylist", from, to);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: reorderPlaylist JS invoke failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Set song status via SignalR.
    /// </summary>
    public async Task SetSongStatusAsync(string itemId, int status)
    {
        if (_sessionBridgeModule == null) return;

        try
        {
            await _sessionBridgeModule.InvokeVoidAsync("setSongStatus", itemId, status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: setSongStatus JS invoke failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Advance to next song via SignalR.
    /// </summary>
    public async Task AdvanceToNextSongAsync()
    {
        if (_sessionBridgeModule == null) return;

        try
        {
            await _sessionBridgeModule.InvokeVoidAsync("advanceToNextSong");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: advanceToNextSong JS invoke failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Complete current song without advancing to next song via SignalR.
    /// </summary>
    public async Task CompleteCurrentSongAsync()
    {
        if (_sessionBridgeModule == null) return;

        try
        {
            await _sessionBridgeModule.InvokeVoidAsync("completeCurrentSong");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: completeCurrentSong JS invoke failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all queued and up-next songs via SignalR, preserving the currently playing song.
    /// </summary>
    public async Task ClearQueueAsync()
    {
        if (_sessionBridgeModule == null) return;

        try
        {
            await _sessionBridgeModule.InvokeVoidAsync("clearQueue");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionService: clearQueue JS invoke failed: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sessionBridgeModule != null)
        {
            await _sessionBridgeModule.DisposeAsync();
        }

        if (_stateUpdateDotNetRef != null)
        {
            _stateUpdateDotNetRef.Dispose();
            _stateUpdateDotNetRef = null;
        }
    }
}
