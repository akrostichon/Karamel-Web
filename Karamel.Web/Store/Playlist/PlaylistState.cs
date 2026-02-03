using Fluxor;
using Karamel.Web.Contracts;

namespace Karamel.Web.Store.Playlist;

[FeatureState]
public record PlaylistState
{
    public List<PlaylistItemDto> Items { get; init; } = new List<PlaylistItemDto>();
    public PlaylistItemDto? CurrentSong { get; init; }
}
