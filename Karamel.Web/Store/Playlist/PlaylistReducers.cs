using Fluxor;

namespace Karamel.Web.Store.Playlist;

public static class PlaylistReducers
{
    [ReducerMethod]
    public static PlaylistState ReduceAddToPlaylistSuccessAction(PlaylistState state, AddToPlaylistSuccessAction action)
    {
        // No-op: SignalR broadcast will update state via UpdatePlaylistFromBroadcastAction
        return state;
    }

    [ReducerMethod]
    public static PlaylistState ReduceRemoveSongAction(PlaylistState state, RemoveSongAction action)
    {
        // No-op: SignalR broadcast will update state via UpdatePlaylistFromBroadcastAction
        return state;
    }

    [ReducerMethod]
    public static PlaylistState ReduceReorderPlaylistAction(PlaylistState state, ReorderPlaylistAction action)
    {
        // No-op: SignalR broadcast will update state via UpdatePlaylistFromBroadcastAction
        return state;
    }

    [ReducerMethod]
    public static PlaylistState ReduceNextSongAction(PlaylistState state, NextSongAction action)
    {
        // No-op: Use AdvanceToNextSongAction instead (triggers SignalR)
        return state;
    }

    [ReducerMethod]
    public static PlaylistState ReduceClearPlaylistAction(PlaylistState state, ClearPlaylistAction action)
    {
        // No-op: Backend ClearQueueAsync handles clearing and broadcasts update via SignalR
        return state;
    }

    [ReducerMethod]
    public static PlaylistState ReduceUpdatePlaylistFromBroadcastAction(PlaylistState state, UpdatePlaylistFromBroadcastAction action)
    {
        try
        {
            Console.WriteLine($"PlaylistReducers: UpdatePlaylistFromBroadcastAction received with {action.Items?.Count ?? 0} items, CurrentSong={action.CurrentSong?.Id}");
        }
        catch { }
        return state with
        {
            Items = action.Items ?? [],
            CurrentSong = action.CurrentSong
        };
    }

    [ReducerMethod]
    public static PlaylistState ReduceClearCurrentSongAction(PlaylistState state, ClearCurrentSongAction action)
    {
        // No-op: Use AdvanceToNextSongAction instead
        return state;
    }

    [ReducerMethod]
    public static PlaylistState ReduceSetSongStatusAction(PlaylistState state, SetSongStatusAction action)
    {
        // No-op: SignalR broadcast will update state
        return state;
    }

    [ReducerMethod]
    public static PlaylistState ReduceAdvanceToNextSongAction(PlaylistState state, AdvanceToNextSongAction action)
    {
        // No-op: SignalR broadcast will update state
        return state;
    }
}
