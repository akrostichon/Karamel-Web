using Fluxor;
using Karamel.Web.Models;

namespace Karamel.Web.Store.Playlist;

public static class PlaylistReducers
{
    private const int MaxSongsPerSinger = 10;

    [ReducerMethod]
    public static PlaylistState ReduceAddToPlaylistSuccessAction(PlaylistState state, AddToPlaylistSuccessAction action)
    {
        var newQueue = new Queue<Song>(state.Queue);
        newQueue.Enqueue(action.Song);

        var singerName = action.Song.AddedBySinger ?? "Unknown";
        var newCounts = new Dictionary<string, int>(state.SingerSongCounts);
        newCounts[singerName] = newCounts.GetValueOrDefault(singerName, 0) + 1;

        return state with
        {
            Queue = newQueue,
            SingerSongCounts = newCounts
        };
    }

    [ReducerMethod]
    public static PlaylistState ReduceRemoveSongAction(PlaylistState state, RemoveSongAction action)
    {
        var songs = state.Queue.ToList();
        var songToRemove = songs.FirstOrDefault(s => s.Id == action.SongId);
        
        if (songToRemove == null)
            return state;

        songs.Remove(songToRemove);
        var newQueue = new Queue<Song>(songs);

        // Update singer song counts
        var singerName = songToRemove.AddedBySinger ?? "Unknown";
        var newCounts = new Dictionary<string, int>(state.SingerSongCounts);
        if (newCounts.ContainsKey(singerName))
        {
            newCounts[singerName] = Math.Max(0, newCounts[singerName] - 1);
            if (newCounts[singerName] == 0)
                newCounts.Remove(singerName);
        }

        return state with
        {
            Queue = newQueue,
            SingerSongCounts = newCounts
        };
    }

    [ReducerMethod]
    public static PlaylistState ReduceReorderPlaylistAction(PlaylistState state, ReorderPlaylistAction action)
    {
        var songs = state.Queue.ToList();
        
        if (action.OldIndex < 0 || action.OldIndex >= songs.Count ||
            action.NewIndex < 0 || action.NewIndex >= songs.Count)
            return state;

        var song = songs[action.OldIndex];
        songs.RemoveAt(action.OldIndex);
        songs.Insert(action.NewIndex, song);

        return state with
        {
            Queue = new Queue<Song>(songs)
        };
    }

    [ReducerMethod]
    public static PlaylistState ReduceNextSongAction(PlaylistState state, NextSongAction action)
    {
        if (state.Queue.Count == 0)
            return state with
            {
                CurrentSong = null,
                CurrentSingerName = null
            };

        var nextSong = state.Queue.Peek();
        var newQueue = new Queue<Song>(state.Queue);
        newQueue.Dequeue();

        // Update singer song counts
        var singerName = nextSong.AddedBySinger ?? "Unknown";
        var newCounts = new Dictionary<string, int>(state.SingerSongCounts);
        if (newCounts.ContainsKey(singerName))
        {
            newCounts[singerName] = Math.Max(0, newCounts[singerName] - 1);
            if (newCounts[singerName] == 0)
                newCounts.Remove(singerName);
        }

        return state with
        {
            Queue = newQueue,
            CurrentSong = nextSong,
            CurrentSingerName = nextSong.AddedBySinger,
            SingerSongCounts = newCounts
        };
    }

    [ReducerMethod]
    public static PlaylistState ReduceClearPlaylistAction(PlaylistState state, ClearPlaylistAction action) =>
        state with
        {
            Queue = new Queue<Song>(),
            CurrentSong = null,
            CurrentSingerName = null,
            SingerSongCounts = new Dictionary<string, int>()
        };

    [ReducerMethod]
    public static PlaylistState ReduceUpdatePlaylistFromBroadcastAction(PlaylistState state, UpdatePlaylistFromBroadcastAction action) =>
        state with
        {
            Queue = new Queue<Song>(action.Queue),
            SingerSongCounts = action.SingerSongCounts
        };
}
