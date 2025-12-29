using Fluxor;

namespace Karamel.Web.Store.Playlist;

public class PlaylistEffects(IState<PlaylistState> playlistState)
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
}
