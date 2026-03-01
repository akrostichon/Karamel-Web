using System.Text.Json.Serialization;

namespace Karamel.Web.Contracts;

/// <summary>
/// Represents a playlist item with status information from the backend.
/// Matches the SignalR DTO structure from PlaylistHub.
/// MINIMAL STRUCTURE - no private file paths sent to server.
/// Components must look up full Song from LibraryState using SongId.
/// </summary>
public record PlaylistItemDto(
    [property: JsonPropertyName("id")] string Id,              // Playlist item ID (for status updates)
    [property: JsonPropertyName("songId")] string? SongId,     // Song ID (for library lookup - REQUIRED for unique identification)
    [property: JsonPropertyName("artist")] string Artist,      // For display when Song not in library
    [property: JsonPropertyName("title")] string Title,        // For display when Song not in library
    [property: JsonPropertyName("singerName")] string? SingerName,  // Who added to playlist
    [property: JsonPropertyName("position")] int Position,     // Order in playlist
    [property: JsonPropertyName("status")] int Status,         // 0=Queued, 1=UpNext, 2=NowPlaying, 3=Completed
    [property: JsonPropertyName("durationSeconds")] int DurationSeconds = 0  // Duration in seconds; 0 = unknown
);

/// <summary>
/// Song status enum matching backend SongStatus enum.
/// </summary>
public enum SongStatus
{
    Queued = 0,
    UpNext = 1,
    NowPlaying = 2,
    Completed = 3
}
