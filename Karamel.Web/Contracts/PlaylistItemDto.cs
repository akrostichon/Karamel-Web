using System.Text.Json.Serialization;

namespace Karamel.Web.Contracts;

/// <summary>
/// Represents a playlist item with status information from the backend.
/// Matches the SignalR DTO structure from PlaylistHub.
/// INCLUDES ALL SONG FIELDS for playback without library lookup.
/// </summary>
public record PlaylistItemDto(
    [property: JsonPropertyName("id")] string Id,              // Playlist item ID (for status updates)
    [property: JsonPropertyName("songId")] string? SongId,     // Song ID (for library lookup)
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("singerName")] string? SingerName,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("status")] int Status,         // 0=Queued, 1=UpNext, 2=NowPlaying, 3=Completed
    [property: JsonPropertyName("mp3FileName")] string Mp3FileName,
    [property: JsonPropertyName("cdgFileName")] string CdgFileName,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("fullPath")] string? FullPath,
    [property: JsonPropertyName("sourceType")] string? SourceType,
    [property: JsonPropertyName("zipFileName")] string? ZipFileName,
    [property: JsonPropertyName("zipFilePath")] string? ZipFilePath,
    [property: JsonPropertyName("zipEntryMp3Path")] string? ZipEntryMp3Path,
    [property: JsonPropertyName("zipEntryCdgPath")] string? ZipEntryCdgPath,
    [property: JsonPropertyName("addedBySinger")] string? AddedBySinger
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
