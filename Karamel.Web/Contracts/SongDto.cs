using System.Text.Json;
using System.Text.Json.Serialization;
using Karamel.Web.Models;

namespace Karamel.Web.Contracts;

public record SongDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("mp3FileName")] string Mp3FileName,
    [property: JsonPropertyName("cdgFileName")] string CdgFileName,
    [property: JsonPropertyName("sourceType")] string? SourceType,
    [property: JsonPropertyName("zipFileName")] string? ZipFileName,
    [property: JsonPropertyName("zipEntryMp3Path")] string? ZipEntryMp3Path,
    [property: JsonPropertyName("zipEntryCdgPath")] string? ZipEntryCdgPath,
    [property: JsonPropertyName("addedBySinger")] string? AddedBySinger
);

public static class SongConverters
{
    public static SongDto ConvertSongToDto(Song s) => new(
        Id: s.Id.ToString(),
        Artist: s.Artist,
        Title: s.Title,
        Mp3FileName: s.Mp3FileName,
        CdgFileName: s.CdgFileName,
        SourceType: s.SourceType.ToString(),
        ZipFileName: s.ZipFileName,
        ZipEntryMp3Path: s.ZipEntryMp3Path,
        ZipEntryCdgPath: s.ZipEntryCdgPath,
        AddedBySinger: s.AddedBySinger
    );

    public static Song ConvertJsonToSong(JsonElement s) => new Song
    {
        Id = Guid.Parse(s.GetProperty("id").GetString()!),
        Artist = s.GetProperty("artist").GetString() ?? string.Empty,
        Title = s.GetProperty("title").GetString() ?? string.Empty,
        Mp3FileName = s.GetProperty("mp3FileName").GetString() ?? string.Empty,
        CdgFileName = s.GetProperty("cdgFileName").GetString() ?? string.Empty,
        SourceType = s.TryGetProperty("sourceType", out var st) && Enum.TryParse<SongSourceType>(st.GetString(), out var parsed) ? parsed : SongSourceType.Directory,
        ZipFileName = s.TryGetProperty("zipFileName", out var zfn) ? zfn.GetString() : null,
        ZipEntryMp3Path = s.TryGetProperty("zipEntryMp3Path", out var zmp3) ? zmp3.GetString() : null,
        ZipEntryCdgPath = s.TryGetProperty("zipEntryCdgPath", out var zcdg) ? zcdg.GetString() : null,
        AddedBySinger = s.TryGetProperty("addedBySinger", out var singer) ? singer.GetString() : null
    };
}
