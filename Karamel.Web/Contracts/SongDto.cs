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
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("fullPath")] string? FullPath,
    [property: JsonPropertyName("sourceType")] string? SourceType,
    [property: JsonPropertyName("zipFileName")] string? ZipFileName,
    [property: JsonPropertyName("zipEntryMp3Path")] string? ZipEntryMp3Path,
    [property: JsonPropertyName("zipEntryCdgPath")] string? ZipEntryCdgPath,
    [property: JsonPropertyName("zipFilePath")] string? ZipFilePath,
    [property: JsonPropertyName("addedBySinger")] string? AddedBySinger
);

public static class SongConverters
{
    public static SongDto ConvertSongToDto(Song s) => new(
        Id: s.Id.ToString(),
        Artist: s.Artist,
        Title: s.Title,
        Mp3FileName: s.Mp3FileName,
        Path: s.Path,
        FullPath: s.FullPath,
        CdgFileName: s.CdgFileName,
        SourceType: s.SourceType.ToString(),
        ZipFileName: s.ZipFileName,
        ZipEntryMp3Path: s.ZipEntryMp3Path,
        ZipEntryCdgPath: s.ZipEntryCdgPath,
        ZipFilePath: s.ZipFilePath,
        AddedBySinger: s.AddedBySinger
    );

    public static Song ConvertJsonToSong(JsonElement s) => new Song
    {
        Id = Guid.Parse(s.GetProperty("id").GetString()!),
        Artist = s.GetProperty("artist").GetString() ?? string.Empty,
        Title = s.GetProperty("title").GetString() ?? string.Empty,
        Mp3FileName = s.TryGetProperty("mp3FileName", out var mp3) ? mp3.GetString() ?? string.Empty : string.Empty,
        CdgFileName = s.TryGetProperty("cdgFileName", out var cdg) ? cdg.GetString() ?? string.Empty : string.Empty,
        Path = s.TryGetProperty("path", out var p) ? p.GetString() : null,
        FullPath = s.TryGetProperty("fullPath", out var fp) ? fp.GetString() : null,
        SourceType = GetSourceTypeFromJson(s, "sourceType"),
        ZipFileName = s.TryGetProperty("zipFileName", out var zfn) ? zfn.GetString() : null,
        ZipEntryMp3Path = s.TryGetProperty("zipEntryMp3Path", out var zmp3) ? zmp3.GetString() : null,
        ZipEntryCdgPath = s.TryGetProperty("zipEntryCdgPath", out var zcdg) ? zcdg.GetString() : null,
        ZipFilePath = s.TryGetProperty("zipFilePath", out var zfp) ? zfp.GetString() : null,
        AddedBySinger = s.TryGetProperty("addedBySinger", out var singer) ? singer.GetString() : null
    };

    public static Song ConvertDtoToSong(SongDto dto)
    {
        return new Song
        {
            Id = Guid.Parse(dto.Id),
            Artist = dto.Artist ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            Mp3FileName = dto.Mp3FileName ?? string.Empty,
            CdgFileName = dto.CdgFileName ?? string.Empty,
            Path = dto.Path,
            FullPath = dto.FullPath,
            SourceType = GetSongTypeFromDto(dto),
            ZipFileName = dto.ZipFileName,
            ZipEntryMp3Path = dto.ZipEntryMp3Path,
            ZipEntryCdgPath = dto.ZipEntryCdgPath,
            ZipFilePath = dto.ZipFilePath,
            AddedBySinger = dto.AddedBySinger
        };
    }

    private static SongSourceType GetSongTypeFromDto(SongDto dto)
    {
        var sourceTypeParsed = SongSourceType.Directory;
        if (!string.IsNullOrWhiteSpace(dto.SourceType) && Enum.TryParse<SongSourceType>(dto.SourceType, ignoreCase: true, out var st))
        {
            sourceTypeParsed = st;
        }

        return sourceTypeParsed;
    }

    private static SongSourceType GetSourceTypeFromJson(JsonElement parent, string propName)
        {
            if (!parent.TryGetProperty(propName, out var st) || st.ValueKind == JsonValueKind.Null)
                return SongSourceType.Directory;

            try
            {
                if (st.ValueKind == JsonValueKind.String)
                {
                    var s = st.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && Enum.TryParse<SongSourceType>(s, ignoreCase: true, out var parsed))
                        return parsed;
                }
                else if (st.ValueKind == JsonValueKind.Number)
                {
                    if (st.TryGetInt32(out var iv))
                    {
                        if (Enum.IsDefined(typeof(SongSourceType), iv))
                            return (SongSourceType)iv;
                    }
                }
            }
            catch
            {
                // fall through to default
            }

            return SongSourceType.Directory;
        }

}
