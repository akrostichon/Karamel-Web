using System.Text.Json;
using Fluxor;
using Karamel.Web.Contracts;
using Karamel.Web.Models;
using Karamel.Web.Store.Library;
using Karamel.Web.Store.Session;
using Microsoft.JSInterop;

namespace Karamel.Web.Services;

/// <summary>
/// Service for session state restoration orchestration for secondary tabs
/// Orchestrator that returns DTOs - Effects handle action dispatching
/// </summary>
public class PlaylistStateSynchronizer : IPlaylistStateSynchronizer, IAsyncDisposable
{
    private readonly ISessionStorageService _sessionStorage;
    private readonly ISessionApiClient _sessionApiClient;
    private readonly ISongEnrichmentService _songEnrichment;
    private readonly ISignalRConnectionManager _connectionManager;
    private readonly IState<LibraryState> _libraryState;
    private readonly IDispatcher _dispatcher;
    private DotNetObjectReference<PlaylistStateSynchronizer>? _stateUpdateDotNetRef;

    public event Action<BroadcastStateUpdate>? StateUpdateReceived;

    public PlaylistStateSynchronizer(
        ISessionStorageService sessionStorage,
        ISessionApiClient sessionApiClient,
        ISongEnrichmentService songEnrichment,
        ISignalRConnectionManager connectionManager,
        IState<LibraryState> libraryState,
        IDispatcher dispatcher)
    {
        _sessionStorage = sessionStorage;
        _sessionApiClient = sessionApiClient;
        _songEnrichment = songEnrichment;
        _connectionManager = connectionManager;
        _libraryState = libraryState;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Restore session state from sessionStorage (secondary tabs)
    /// Returns (Session, PlaylistItems, CurrentSong) tuple for Effects to dispatch
    /// </summary>
    public async Task<(Session? session, List<PlaylistItemDto>? playlist, SongDto? currentSong)> RestoreSessionStateAsync(Guid sessionId)
    {
        DotNetObjectReference<StateSync>? dotNetRef = null;
        try
        {
#if DEBUG
            Console.WriteLine($"PlaylistStateSynchronizer: Starting to restore session {sessionId}");
#endif
            
            dotNetRef = await WaitForStateSyncAsync();
            var stateJson = await _sessionStorage.ReadSessionStorageAsync(sessionId);
            
            var session = await RestoreSessionConfigAsync(sessionId, stateJson);
            var (playlist, currentSong) = await RestorePlaylistStateAsync(stateJson);
            
            return (session, playlist, currentSong);
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
            return (null, null, null);
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
        var module = await _connectionManager.GetModuleAsync();
        if (module == null)
            throw new InvalidOperationException("SignalR connection not initialized");

        var syncCompletionSource = new TaskCompletionSource<bool>();
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        // Set up event listener for state sync
        var dotNetRef = DotNetObjectReference.Create(new StateSync(syncCompletionSource));
        await module.InvokeVoidAsync("setupStateSyncListener", dotNetRef);
        
        // Wait for sync or timeout
        var syncTask = syncCompletionSource.Task;
        var timeoutTask = Task.Delay(5000, timeoutCts.Token);
        var completedTask = await Task.WhenAny(syncTask, timeoutTask);
        
        if (completedTask == syncTask)
        {
#if DEBUG
            Console.WriteLine($"PlaylistStateSynchronizer: State sync completed");
#endif
        }
        else
        {
#if DEBUG
            Console.WriteLine($"PlaylistStateSynchronizer: State sync timed out, using current sessionStorage");
#endif
        }

        return dotNetRef;
    }

    /// <summary>
    /// Restore session configuration from sessionStorage or backend API
    /// </summary>
    private async Task<Session?> RestoreSessionConfigAsync(Guid sessionId, JsonElement stateJson)
    {
        Console.WriteLine($"[DIAG] PlaylistStateSynchronizer.RestoreSessionConfigAsync: START for sessionId={sessionId}");
        Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: stateJson.ValueKind={stateJson.ValueKind}");
        
        if (stateJson.TryGetProperty("session", out var sessionData) && 
            sessionData.ValueKind != JsonValueKind.Null)
        {
            Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: Found session data in sessionStorage");
            Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: sessionData={sessionData}");
            
            var session = ParseSessionFromJson(sessionId, sessionData);
            
            Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: Parsed session - SessionId={session.SessionId}, RequireSingerName={session.RequireSingerName}");
            Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: Dispatching InitializeSessionAction FROM SESSIONSTORAGE");
            _dispatcher.Dispatch(new InitializeSessionAction(session));
            Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: InitializeSessionAction dispatched successfully");
            return session;
        }
        else
        {
            Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: No session data found in sessionStorage - MULTI-DEVICE scenario");
            Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: Will fetch from backend API...");
            var session = await _sessionApiClient.FetchSessionConfigFromBackendAsync(sessionId);
            if (session != null)
            {
                Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: Dispatching InitializeSessionAction FROM BACKEND");
                _dispatcher.Dispatch(new InitializeSessionAction(session));
                Console.WriteLine($"[DIAG] PlaylistStateSynchronizer: InitializeSessionAction dispatched successfully");
            }
            return session;
        }
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
            FilenamePattern = sessionData.GetProperty("filenamePattern").GetString() ?? "%artist - %title",
            Theme = sessionData.TryGetProperty("theme", out var theme) && theme.ValueKind == JsonValueKind.String
                ? theme.GetString()
                : null
        };
    }

    /// <summary>
    /// Restore playlist state from sessionStorage if available
    /// </summary>
    private async Task<(List<PlaylistItemDto>? playlist, SongDto? currentSong)> RestorePlaylistStateAsync(JsonElement stateJson)
    {
        if (stateJson.TryGetProperty("playlist", out var playlistData) &&
            playlistData.ValueKind != JsonValueKind.Null)
        {
#if DEBUG
            Console.WriteLine($"PlaylistStateSynchronizer: Found playlist data in sessionStorage - initializing state");
#endif
            try
            {
                var result = HandlePlaylistUpdate(playlistData);
                if (result.HasValue)
                {
                    // Convert to DTOs for Effects
                    var itemDtos = result.Value.queue.Select((s, index) => new PlaylistItemDto(
                        Id: Guid.NewGuid().ToString(),
                        SongId: s.Id.ToString(),
                        Artist: s.Artist,
                        Title: s.Title,
                        SingerName: s.AddedBySinger,
                        Position: index,
                        Status: (int)SongStatus.Queued
                    )).ToList();

                    var currentSongDto = result.Value.currentSong != null ? new SongDto(
                        Id: result.Value.currentSong.Id.ToString(),
                        Artist: result.Value.currentSong.Artist,
                        Title: result.Value.currentSong.Title,
                        Mp3FileName: result.Value.currentSong.Mp3FileName,
                        CdgFileName: result.Value.currentSong.CdgFileName,
                        VideoFileName: result.Value.currentSong.VideoFileName,
                        VideoExtension: result.Value.currentSong.VideoExtension,
                        MediaType: result.Value.currentSong.MediaType == MediaType.Video ? "video" : "mp3cdg",
                        Path: result.Value.currentSong.Path,
                        FullPath: result.Value.currentSong.FullPath,
                        SourceType: result.Value.currentSong.SourceType == SongSourceType.Zip ? "zip" : "directory",
                        ZipFileName: result.Value.currentSong.ZipFileName,
                        ZipEntryMp3Path: result.Value.currentSong.ZipEntryMp3Path,
                        ZipEntryCdgPath: result.Value.currentSong.ZipEntryCdgPath,
                        ZipFilePath: result.Value.currentSong.ZipFilePath,
                        AddedBySinger: result.Value.currentSong.AddedBySinger
                    ) : null;

                    return (itemDtos, currentSongDto);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PlaylistStateSynchronizer: Failed to restore playlist from sessionStorage: {ex.Message}");
            }
        }
        else
        {
#if DEBUG
            Console.WriteLine($"PlaylistStateSynchronizer: No playlist data in sessionStorage - will receive initial state from SignalR");
#endif
        }
        
        await Task.CompletedTask;
        return (null, null);
    }

    /// <summary>
    /// Setup listener for ongoing state updates from main tab (secondary tabs only)
    /// </summary>
    public async Task SetupStateUpdateListenerAsync()
    {
        var module = await _connectionManager.GetModuleAsync();
        if (module == null)
            return;

        if (_stateUpdateDotNetRef == null)
        {
            _stateUpdateDotNetRef = DotNetObjectReference.Create(this);
        }

#if DEBUG
        Console.WriteLine($"PlaylistStateSynchronizer: Registering state update listener (isMainTab={_connectionManager.IsMainTab})");
#endif
        await module.InvokeVoidAsync("setupStateUpdateListener", _stateUpdateDotNetRef);
    }

    /// <summary>
    /// Handle state update from broadcast (called by JavaScript via JSInvokable)
    /// </summary>
    [JSInvokable("HandleBroadcastMessage")]
    public void HandleBroadcastMessage(string type, JsonElement data)
    {
        try
        {
#if DEBUG
            Console.WriteLine($"PlaylistStateSynchronizer: Received state update: {type}. PayloadKind={data.ValueKind}");
#endif
            
            switch (type)
            {
                case "playlist-updated":
                    var playlistResult = HandlePlaylistUpdate(data);
                    if (playlistResult.HasValue)
                    {
                        var playlistUpdate = new PlaylistBroadcastUpdate(
                            playlistResult.Value.queue,
                            playlistResult.Value.currentSong,
                            playlistResult.Value.singerCounts,
                            playlistResult.Value.currentSingerName);

                        StateUpdateReceived?.Invoke(new BroadcastStateUpdate(
                            type,
                            playlistUpdate,
                            null,
                            null));
                    }
                    break;
                case "session-settings":
                    var session = HandleSessionSettingsUpdate(data);
                    if (session is not null)
                    {
                        StateUpdateReceived?.Invoke(new BroadcastStateUpdate(
                            type,
                            null,
                            session,
                            null));
                    }
                    break;
                case "current-song":
                    var songUpdate = HandleCurrentSongUpdate(data);
                    if (songUpdate.HasValue)
                    {
                        var currentSongUpdate = new CurrentSongBroadcastUpdate(
                            songUpdate.Value.song,
                            songUpdate.Value.singerName);

                        StateUpdateReceived?.Invoke(new BroadcastStateUpdate(
                            type,
                            null,
                            null,
                            currentSongUpdate));
                    }
                    break;
                case "session-paused":
                case "session-resumed":
                    // Lifecycle events carry no payload – forward type only so Effects can dispatch
                    StateUpdateReceived?.Invoke(new BroadcastStateUpdate(type, null, null, null));
                    break;
                case "config-updated":
                    var configUpdate = HandleConfigUpdate(data);
                    if (configUpdate is not null)
                    {
                        StateUpdateReceived?.Invoke(new BroadcastStateUpdate(type, null, null, null, configUpdate));
                    }
                    break;
                default:
                    Console.WriteLine($"PlaylistStateSynchronizer: Unknown state update type: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PlaylistStateSynchronizer: Error handling state update: {ex.Message}");
        }
    }

    [JSInvokable("OnStateUpdated")]
    public void OnStateUpdated(string type, JsonElement data)
    {
        HandleBroadcastMessage(type, data);
    }

    /// <summary>
    /// Handle playlist update from broadcast
    /// </summary>
    private (List<Song> queue, Song? currentSong, Dictionary<string, int> singerCounts, string? currentSingerName)? HandlePlaylistUpdate(JsonElement data)
    {
        try
        {
            // Extract queue
            if (!data.TryGetProperty("queue", out var queueArray))
                return null;

#if DEBUG
            Console.WriteLine($"PlaylistStateSynchronizer: Playlist update contains queue with {queueArray.GetArrayLength()} items");
#endif

            // 1. Extract and convert queue
            var queue = ExtractQueueFromJson(queueArray);

            // 2. Enrich songs if main tab
            Dictionary<Guid, Song>? libraryLookup = null;
            if (_connectionManager.IsMainTab && _libraryState.Value.Songs.Count > 0)
            {
#if DEBUG
                Console.WriteLine($"PlaylistStateSynchronizer: Enriching {queue.Count} songs with file information from local library ({_libraryState.Value.Songs.Count} songs available)");
#endif
                libraryLookup = _songEnrichment.BuildLibraryLookup(_libraryState.Value.Songs);
                _songEnrichment.EnrichSongsWithLibraryFiles(queue, libraryLookup);
            }

            // 3. Extract singer song counts
            var singerSongCounts = ExtractSingerSongCounts(data);

            // 4. Log first item for diagnostics
            LogFirstQueueItem(queue);

            // 5. Extract current song and singer name
            var currentSong = ExtractCurrentSongFromJson(data, libraryLookup);
            var currentSingerName = ExtractCurrentSingerName(data);

            return (queue, currentSong, singerSongCounts, currentSingerName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PlaylistStateSynchronizer: Error parsing playlist update: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Handle session settings update from broadcast
    /// </summary>
    private Session? HandleSessionSettingsUpdate(JsonElement data)
    {
#if DEBUG
        Console.WriteLine($"PlaylistStateSynchronizer: Session settings update received");
#endif
        if (!data.TryGetProperty("sessionId", out var sessionIdElement))
        {
            return null;
        }

        if (!Guid.TryParse(sessionIdElement.GetString(), out var sessionId))
        {
            return null;
        }

        return new Session
        {
            SessionId = sessionId,
            RequireSingerName = data.TryGetProperty("requireSingerName", out var requireSingerName)
                && requireSingerName.ValueKind == JsonValueKind.True,
            AllowSingersToReorder = data.TryGetProperty("allowSingerReorder", out var allowSingerReorder)
                && allowSingerReorder.ValueKind == JsonValueKind.True,
            PauseBetweenSongs = !data.TryGetProperty("pauseBetweenSongs", out var pauseBetweenSongs)
                || pauseBetweenSongs.ValueKind != JsonValueKind.False,
            PauseBetweenSongsSeconds = data.TryGetProperty("pauseBetweenSongsSeconds", out var pauseBetweenSongsSeconds)
                && pauseBetweenSongsSeconds.ValueKind == JsonValueKind.Number
                ? pauseBetweenSongsSeconds.GetInt32()
                : 5,
            FilenamePattern = data.TryGetProperty("filenamePattern", out var filenamePattern)
                && filenamePattern.ValueKind == JsonValueKind.String
                ? filenamePattern.GetString() ?? "%artist - %title"
                : "%artist - %title",
            Theme = data.TryGetProperty("theme", out var theme) && theme.ValueKind == JsonValueKind.String
                ? theme.GetString()
                : null
        };
    }

    /// <summary>
    /// Handle runtime config update from SignalR ReceiveConfigUpdated broadcast.
    /// The payload matches the backend SessionConfigDto shape (camelCase JSON).
    /// </summary>
    private SessionConfigBroadcastUpdate? HandleConfigUpdate(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Null || data.ValueKind == JsonValueKind.Undefined)
            return null;

        var requireSingerName = data.TryGetProperty("requireSingerName", out var rsn)
            && rsn.ValueKind == JsonValueKind.True;
        var allowSingersToReorder = !data.TryGetProperty("allowSingersToReorder", out var asr)
            || asr.ValueKind != JsonValueKind.False;
        var pauseBetweenSongsSeconds = data.TryGetProperty("pauseBetweenSongsSeconds", out var pbs)
            && pbs.ValueKind == JsonValueKind.Number
            ? pbs.GetInt32()
            : 0;
        var theme = data.TryGetProperty("theme", out var themeEl) && themeEl.ValueKind == JsonValueKind.String
            ? themeEl.GetString()
            : null;

        return new SessionConfigBroadcastUpdate(requireSingerName, allowSingersToReorder, pauseBetweenSongsSeconds, theme);
    }

    /// <summary>
    /// Handle current song update from broadcast
    /// </summary>
    private (Song? song, string? singerName)? HandleCurrentSongUpdate(JsonElement data)
    {
#if DEBUG
        Console.WriteLine($"PlaylistStateSynchronizer: Current song update received");
#endif
        if (data.ValueKind == JsonValueKind.Null)
        {
            return (null, null);
        }

        string? singerName = null;
        if (data.TryGetProperty("singerName", out var singerNameElement)
            && singerNameElement.ValueKind == JsonValueKind.String)
        {
            singerName = singerNameElement.GetString();
        }

        if (!data.TryGetProperty("song", out var songElement) || songElement.ValueKind == JsonValueKind.Null)
        {
            return (null, singerName);
        }

        var song = SongConverters.ConvertJsonToSong(songElement);
        return (song, singerName);
    }

    /// <summary>
    /// Extract and convert queue array from JSON to List of Songs
    /// </summary>
    private List<Song> ExtractQueueFromJson(JsonElement queueArray)
    {
        return queueArray.EnumerateArray().Select(SongConverters.ConvertJsonToSong).ToList();
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
                bool needsEnrichment = false;
                if (currentSong != null)
                {
                    if (currentSong.MediaType == MediaType.Mp3Cdg)
                    {
                        needsEnrichment = string.IsNullOrEmpty(currentSong.Mp3FileName) || string.IsNullOrEmpty(currentSong.CdgFileName);
                    }
                    else if (currentSong.MediaType == MediaType.Video)
                    {
                        needsEnrichment = string.IsNullOrEmpty(currentSong.VideoFileName);
                    }
                }
                
                if (_connectionManager.IsMainTab && currentSong != null && libraryLookup != null && needsEnrichment)
                {
                    if (libraryLookup.TryGetValue(currentSong.Id, out var libraryMatch))
                    {
                        currentSong = libraryMatch with { AddedBySinger = currentSong.AddedBySinger };
#if DEBUG
                        Console.WriteLine($"PlaylistStateSynchronizer: Enriched currentSong '{currentSong.Artist} - {currentSong.Title}'");
#endif
                    }
                }
                
                return currentSong;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PlaylistStateSynchronizer: Error parsing currentSong: {ex.Message}");
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
            Console.WriteLine($"PlaylistStateSynchronizer: First queued song: id={first.Id} artist={first.Artist} title={first.Title} addedBy={first.AddedBySinger}");
        }
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

    public async ValueTask DisposeAsync()
    {
        if (_stateUpdateDotNetRef != null)
        {
            _stateUpdateDotNetRef.Dispose();
            _stateUpdateDotNetRef = null;
        }
        await Task.CompletedTask;
    }
}
