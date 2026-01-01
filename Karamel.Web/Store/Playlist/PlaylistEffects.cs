using Fluxor;
using Karamel.Web.Services;

namespace Karamel.Web.Store.Playlist;

public class PlaylistEffects(IState<PlaylistState> playlistState, ISessionService sessionService)
{
    private const int MaxSongsPerSinger = 10;

    [EffectMethod]
    public Task HandleAddToPlaylistAction(AddToPlaylistAction action, IDispatcher dispatcher)
    {
        var state = playlistState.Value;
        var singerName = action.SingerName ?? "Unknown";
        var currentCount = state.SingerSongCounts.GetValueOrDefault(singerName, 0);

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
            var sent = await sessionService.AddItemToPlaylistAsync(action.Song);
            if (!sent)
            {
                await sessionService.BroadcastPlaylistUpdatedAsync();
            }
        }
        catch
        {
            await sessionService.BroadcastPlaylistUpdatedAsync();
        }
    }

    [EffectMethod]
    public async Task HandleRemoveSongAction(RemoveSongAction action, IDispatcher dispatcher)
    {
        try
        {
            var sent = await sessionService.RemoveItemFromPlaylistAsync(action.SongId);
            if (!sent)
            {
                await sessionService.BroadcastPlaylistUpdatedAsync();
            }
        }
        catch
        {
            await sessionService.BroadcastPlaylistUpdatedAsync();
        }
    }

    [EffectMethod]
    public async Task HandleNextSongAction(NextSongAction action, IDispatcher dispatcher)
    {
        // Broadcast playlist update after advancing to next song
        await sessionService.BroadcastPlaylistUpdatedAsync();
    }

    [EffectMethod]
    public async Task HandleClearPlaylistAction(ClearPlaylistAction action, IDispatcher dispatcher)
    {
        // Broadcast playlist update after clearing
        await sessionService.BroadcastPlaylistUpdatedAsync();
    }

    [EffectMethod]
    public async Task HandleReorderPlaylistAction(ReorderPlaylistAction action, IDispatcher dispatcher)
    {
        try
        {
            // Use current playlist order from state as the new order
            var currentQueue = playlistState.Value.Queue;
            var sent = await sessionService.ReorderPlaylistAsync(currentQueue);
            if (!sent)
            {
                await sessionService.BroadcastPlaylistUpdatedAsync();
            }
        }
        catch
        {
            await sessionService.BroadcastPlaylistUpdatedAsync();
        }
    }
}
