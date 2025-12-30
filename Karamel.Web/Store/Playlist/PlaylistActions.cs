using Karamel.Web.Models;

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
public record UpdatePlaylistFromBroadcastAction(List<Song> Queue, Dictionary<string, int> SingerSongCounts, Song? CurrentSong = null, string? CurrentSingerName = null);
