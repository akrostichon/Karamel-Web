using Karamel.Web.Models;
using Karamel.Web.Contracts;

namespace Karamel.Web.Store.Playlist;

// Actions
public record AddToPlaylistAction(Song Song, string? SingerName = null);
public record AddToPlaylistSuccessAction(Song Song);
public record AddToPlaylistFailureAction(string ErrorMessage);
public record RemoveSongAction(Guid SongId);
public record ReorderPlaylistAction(int OldIndex, int NewIndex);
public record NextSongAction();
public record ClearCurrentSongAction();
public record ClearPlaylistAction();
// SignalR updates - receives playlist items from backend
public record UpdatePlaylistFromBroadcastAction(List<PlaylistItemDto> Items, PlaylistItemDto? CurrentSong);
public record SetSongStatusAction(string ItemId, int Status);
public record AdvanceToNextSongAction();
