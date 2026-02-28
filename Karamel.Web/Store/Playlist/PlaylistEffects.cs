using Fluxor;
using Karamel.Web.Contracts;
using Karamel.Web.Services;
using Karamel.Web.Store.Session;

namespace Karamel.Web.Store.Playlist;

public class PlaylistEffects
{
    private readonly IState<PlaylistState> _playlistState;
    private readonly IState<SessionState> _sessionState;
    private readonly ISignalRPlaylistBridge _signalRBridge;
    private readonly IDispatcher _dispatcher;
    private const int MaxSongsPerSinger = 10;

    public PlaylistEffects(
        IState<PlaylistState> playlistState,
        IState<SessionState> sessionState,
        ISignalRPlaylistBridge signalRBridge,
        IPlaylistStateSynchronizer playlistStateSynchronizer,
        IDispatcher dispatcher)
    {
        _playlistState = playlistState;
        _sessionState = sessionState;
        _signalRBridge = signalRBridge;
        _dispatcher = dispatcher;
        playlistStateSynchronizer.StateUpdateReceived += OnStateUpdateReceived;
    }

    [EffectMethod]
    public Task HandleAddToPlaylistAction(AddToPlaylistAction action, IDispatcher dispatcher)
    {
        var state = _playlistState.Value;
        var singerName = action.SingerName ?? "Unknown";

        // Enforce singer-name requirement when the session flag is enabled
        if ((_sessionState.Value.CurrentSession?.RequireSingerName ?? false)
            && string.IsNullOrWhiteSpace(action.SingerName))
        {
            dispatcher.Dispatch(new AddToPlaylistFailureAction("Singer name is required to add a song."));
            return Task.CompletedTask;
        }

        // Calculate current count on-demand from Items
        var currentCount = state.Items.Count(i => i.SingerName == singerName && i.Status != 3); // 3=Completed

        if (currentCount >= MaxSongsPerSinger)
        {
            dispatcher.Dispatch(new AddToPlaylistFailureAction(
                $"Maximum {MaxSongsPerSinger} songs per singer reached"));
            return Task.CompletedTask;
        }

        // Create a new song with the singer name
        var songWithSinger = action.Song with { AddedBySinger = action.SingerName };
        dispatcher.Dispatch(new AddToPlaylistSuccessAction(songWithSinger));
        
        return Task.CompletedTask;
    }

    [EffectMethod]
    public async Task HandleAddToPlaylistSuccessAction(AddToPlaylistSuccessAction action, IDispatcher dispatcher)
    {
        // Try to use server-side RPC via SignalR; fallback to local broadcast if unavailable
        try
        {
            var sent = await _signalRBridge.AddItemToPlaylistAsync(action.Song);
            if (!sent)
            {
                await _signalRBridge.BroadcastPlaylistUpdatedAsync();
            }
        }
        catch
        {
            await _signalRBridge.BroadcastPlaylistUpdatedAsync();
        }
    }

    [EffectMethod]
    public async Task HandleRemoveSongAction(RemoveSongAction action, IDispatcher dispatcher)
    {
        try
        {
            var sent = await _signalRBridge.RemoveItemFromPlaylistAsync(action.SongId);
            if (!sent)
            {
                await _signalRBridge.BroadcastPlaylistUpdatedAsync();
            }
        }
        catch
        {
            await _signalRBridge.BroadcastPlaylistUpdatedAsync();
        }
    }

    [EffectMethod]
    public async Task HandleNextSongAction(NextSongAction action, IDispatcher dispatcher)
    {
        // Broadcast playlist update after advancing to next song
        await _signalRBridge.BroadcastPlaylistUpdatedAsync();
    }

    [EffectMethod]
    public async Task HandleClearPlaylistAction(ClearPlaylistAction action, IDispatcher dispatcher)
    {
        // Call backend to clear queued and up-next songs (preserves currently playing song)
        await _signalRBridge.ClearQueueAsync();
    }

    /// <summary>
    /// When an admin initiates a pause, invoke the hub so all clients receive ReceiveSessionPaused.
    /// Broadcast-triggered dispatches (IsAdminInitiated=false) skip the hub call to prevent loops.
    /// </summary>
    [EffectMethod]
    public async Task HandlePauseSessionAction(PauseSessionAction action, IDispatcher dispatcher)
    {
        if (action.IsAdminInitiated)
        {
            await _signalRBridge.PauseSessionAsync();
        }
    }

    /// <summary>
    /// When an admin initiates a resume, invoke the hub so all clients receive ReceiveSessionResumed.
    /// Broadcast-triggered dispatches (IsAdminInitiated=false) skip the hub call to prevent loops.
    /// </summary>
    [EffectMethod]
    public async Task HandleResumeSessionAction(ResumeSessionAction action, IDispatcher dispatcher)
    {
        if (action.IsAdminInitiated)
        {
            await _signalRBridge.ResumeSessionAsync();
        }
    }

    /// <summary>
    /// When an admin saves the session configuration, invoke the hub UpdateSessionConfigAsync.
    /// The hub persists the config and broadcasts ReceiveConfigUpdated to all clients.
    /// </summary>
    [EffectMethod]
    public async Task HandleSaveSessionConfigAction(SaveSessionConfigAction action, IDispatcher dispatcher)
    {
        await _signalRBridge.UpdateSessionConfigAsync(
            action.RequireSingerName,
            action.AllowSingersToReorder,
            action.PauseBetweenSongsSeconds,
            action.Theme);
    }

    [EffectMethod]
    public async Task HandleReorderPlaylistAction(ReorderPlaylistAction action, IDispatcher dispatcher)
    {
        try
        {
            // ReorderPlaylistAsync handles the reordering logic internally
            await _signalRBridge.ReorderPlaylistAsync(action.OldIndex, action.NewIndex);
            // SignalR broadcast will update state
        }
        catch
        {
            // Errors logged by SignalRPlaylistBridge
        }
    }

    [EffectMethod]
    public async Task HandleSetSongStatusAction(SetSongStatusAction action, IDispatcher dispatcher)
    {
        try
        {
            await _signalRBridge.SetSongStatusAsync(action.ItemId, action.Status);
            // SignalR broadcast will update state
        }
        catch
        {
            // Errors logged by SignalRPlaylistBridge
        }
    }

    [EffectMethod]
    public async Task HandleAdvanceToNextSongAction(AdvanceToNextSongAction action, IDispatcher dispatcher)
    {
        // Suppress automatic and manual advancement while session is paused
        if (_sessionState.Value.IsPaused)
            return;

        try
        {
            await _signalRBridge.AdvanceToNextSongAsync();
            // SignalR broadcast will update state
        }
        catch
        {
            // Errors logged by SignalRPlaylistBridge
        }
    }

    [EffectMethod]
    public async Task HandleCompleteCurrentSongAction(CompleteCurrentSongAction action, IDispatcher dispatcher)
    {
        try
        {
            await _signalRBridge.CompleteCurrentSongAsync();
            // SignalR broadcast will update state
        }
        catch
        {
            // Errors logged by SignalRPlaylistBridge
        }
    }

    private void OnStateUpdateReceived(BroadcastStateUpdate update)
    {
        switch (update.Type)
        {
            case "playlist-updated":
                if (update.Playlist is null) return;

                var itemDtos = update.Playlist.Queue.Select((song, index) => new PlaylistItemDto(
                    Id: Guid.NewGuid().ToString(),
                    SongId: song.Id.ToString(),
                    Artist: song.Artist,
                    Title: song.Title,
                    SingerName: song.AddedBySinger,
                    Position: index,
                    Status: (int)SongStatus.Queued
                )).ToList();

                var currentSongDto = update.Playlist.CurrentSong is null
                    ? null
                    : new PlaylistItemDto(
                        Id: Guid.NewGuid().ToString(),
                        SongId: update.Playlist.CurrentSong.Id.ToString(),
                        Artist: update.Playlist.CurrentSong.Artist,
                        Title: update.Playlist.CurrentSong.Title,
                        SingerName: update.Playlist.CurrentSong.AddedBySinger,
                        Position: 0,
                        Status: (int)SongStatus.NowPlaying
                    );

                _dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(itemDtos, currentSongDto));
                break;

            case "session-paused":
                _dispatcher.Dispatch(new PauseSessionAction(IsAdminInitiated: false));
                break;

            case "session-resumed":
                _dispatcher.Dispatch(new ResumeSessionAction(IsAdminInitiated: false));
                break;

            case "config-updated":
                if (update.Config is not null)
                {
                    _dispatcher.Dispatch(new SessionConfigUpdatedAction(
                        update.Config.RequireSingerName,
                        update.Config.AllowSingersToReorder,
                        update.Config.PauseBetweenSongsSeconds,
                        update.Config.Theme));
                }
                break;
        }
    }
}
