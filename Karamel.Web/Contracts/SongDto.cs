using System.Text.Json;
using System.Text.Json.Serialization;
using Karamel.Web.Models;

namespace Karamel.Web.Contracts;

public record SongDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("mp3FileName")] string? Mp3FileName,
    [property: JsonPropertyName("cdgFileName")] string? CdgFileName,
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
    /// <summary>
    /// Convert Song to sanitized upload DTO (PRIVACY: excludes filecodes paths).
    /// Use this for uploading to backend - only contains Artist, Title, and future metadata fields.
    /// </summary>
    public static SongUploadDto ConvertSongToUploadDto(Song s) => new(
        Id: s.Id.ToString(),
        Artist: s.Artist,
        Title: s.Title,
        MetadataJson: null  // TODO: Serialize duration, genre when implemented
    );

    /// <summary>
    /// Convert Song to full DTO (includes file paths for internal use).
    /// WARNING: This contains private file paths - use ConvertSongToUploadDto for backend uploads.
    /// </summary>
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

    /// <summary>
    /// Convert JSON from backend to Song model.
   /// PRIVACY: Backend never returns file paths - all path fields will be empty/null.
    /// Secondary tabs use this for display-only (browse/search) without playback capability.
    /// </summary>
    public static Song ConvertJsonToSong(JsonElement s) => new Song
    {
        Id = Guid.Parse(s.GetProperty("id").GetString()!),
        Artist = s.GetProperty("artist").GetString() ?? string.Empty,
        Title = s.GetProperty("title").GetString() ?? string.Empty,
        // Default to Mp3Cdg for backward compatibility
        MediaType = MediaType.Mp3Cdg,
        // PRIVACY: File paths never returned from backend (empty/null for secondary tabs)
        Mp3FileName = null,
        CdgFileName = null,
        VideoFileName = null,
        VideoExtension = null,
        Path = null,
        FullPath = null,
        SourceType = SongSourceType.Directory,
        ZipFileName = null,
        ZipEntryMp3Path = null,
        ZipEntryCdgPath = null,
        ZipFilePath = null,
        AddedBySinger = s.TryGetProperty("addedBySinger", out var singer) ? singer.GetString() : null
    };

    public static Song ConvertDtoToSong(SongDto dto)
    {
        return new Song
        {
            Id = Guid.Parse(dto.Id),
            Artist = dto.Artist ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            // Default to Mp3Cdg for backward compatibility
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = dto.Mp3FileName,
            CdgFileName = dto.CdgFileName,
            VideoFileName = null,
            VideoExtension = null,
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
