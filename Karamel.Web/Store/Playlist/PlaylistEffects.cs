using Fluxor;
using Karamel.Web.Services;

namespace Karamel.Web.Store.Playlist;

public class PlaylistEffects(IState<PlaylistState> playlistState, ISignalRPlaylistBridge signalRBridge)
{
    private const int MaxSongsPerSinger = 10;

    [EffectMethod]
    public Task HandleAddToPlaylistAction(AddToPlaylistAction action, IDispatcher dispatcher)
    {
        var state = playlistState.Value;
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
            var sent = await signalRBridge.AddItemToPlaylistAsync(action.Song);
            if (!sent)
            {
                await signalRBridge.BroadcastPlaylistUpdatedAsync();
            }
        }
        catch
        {
            await signalRBridge.BroadcastPlaylistUpdatedAsync();
        }
    }

    [EffectMethod]
    public async Task HandleRemoveSongAction(RemoveSongAction action, IDispatcher dispatcher)
    {
        try
        {
            var sent = await signalRBridge.RemoveItemFromPlaylistAsync(action.SongId);
            if (!sent)
            {
                await signalRBridge.BroadcastPlaylistUpdatedAsync();
            }
        }
        catch
        {
            await signalRBridge.BroadcastPlaylistUpdatedAsync();
        }
    }

    [EffectMethod]
    public async Task HandleNextSongAction(NextSongAction action, IDispatcher dispatcher)
    {
        // Broadcast playlist update after advancing to next song
        await signalRBridge.BroadcastPlaylistUpdatedAsync();
    }

    [EffectMethod]
    public async Task HandleClearPlaylistAction(ClearPlaylistAction action, IDispatcher dispatcher)
    {
        // Call backend to clear queued and up-next songs (preserves currently playing song)
        await signalRBridge.ClearQueueAsync();
    }

    [EffectMethod]
    public async Task HandleReorderPlaylistAction(ReorderPlaylistAction action, IDispatcher dispatcher)
    {
        try
        {
            // ReorderPlaylistAsync handles the reordering logic internally
            var sent = await signalRBridge.ReorderPlaylistAsync(action.OldIndex, action.NewIndex);
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
            await signalRBridge.SetSongStatusAsync(action.ItemId, action.Status);
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
            await signalRBridge.AdvanceToNextSongAsync();
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
            await signalRBridge.CompleteCurrentSongAsync();
            // SignalR broadcast will update state
        }
        catch
        {
            // Errors logged by SignalRPlaylistBridge
        }
    }
}
