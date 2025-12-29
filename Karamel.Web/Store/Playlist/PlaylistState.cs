using Fluxor;
using Karamel.Web.Models;

namespace Karamel.Web.Store.Playlist;

[FeatureState]
public record PlaylistState
{
    public Queue<Song> Queue { get; init; } = new Queue<Song>();
    public Song? CurrentSong { get; init; }
    public string? CurrentSingerName { get; init; }
    public IReadOnlyDictionary<string, int> SingerSongCounts { get; init; } = 
        new Dictionary<string, int>();
}
