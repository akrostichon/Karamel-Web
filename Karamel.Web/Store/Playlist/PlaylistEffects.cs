using Fluxor;
using Karamel.Web.Contracts;
using Karamel.Web.Services;

namespace Karamel.Web.Store.Playlist;

public class PlaylistEffects
{
    private readonly IState<PlaylistState> _playlistState;
    private readonly ISignalRPlaylistBridge _signalRBridge;
    private readonly IDispatcher _dispatcher;
    private const int MaxSongsPerSinger = 10;

    public PlaylistEffects(
        IState<PlaylistState> playlistState,
        ISignalRPlaylistBridge signalRBridge,
        IPlaylistStateSynchronizer playlistStateSynchronizer,
        IDispatcher dispatcher)
    {
        _playlistState = playlistState;
        _signalRBridge = signalRBridge;
        _dispatcher = dispatcher;
        playlistStateSynchronizer.StateUpdateReceived += OnStateUpdateReceived;
    }

    [EffectMethod]
    public Task HandleAddToPlaylistAction(AddToPlaylistAction action, IDispatcher dispatcher)
    {
        var state = _playlistState.Value;
        var singerName = action.SingerName ?? "Unknown";
        
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
        if (update.Type != "playlist-updated" || update.Playlist is null)
        {
            return;
        }

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
    }
}
