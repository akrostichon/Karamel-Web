using Karamel.Web.Contracts;
using Karamel.Web.Models;

namespace Karamel.Web.Tests.TestHelpers;

/// <summary>
/// Factory for creating test data objects.
/// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// Creates a PlaylistItemDto from a Song for testing purposes.
    /// </summary>
    public static PlaylistItemDto CreatePlaylistItem(Song song, int position = 0, int status = 0)
    {
        return new PlaylistItemDto(
            Id: Guid.NewGuid().ToString(),
            SongId: song.Id.ToString(),
            Artist: song.Artist,
            Title: song.Title,
            SingerName: song.AddedBySinger,
            Position: position,
            Status: status
        );
    }

    /// <summary>
    /// Creates a list of PlaylistItemDtos from a collection of Songs.
    /// </summary>
    public static List<PlaylistItemDto> CreatePlaylistItems(IEnumerable<Song> songs, int startingStatus = 0)
    {
        int position = 0;
        return songs.Select(song => CreatePlaylistItem(song, position++, startingStatus)).ToList();
    }
}
