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
    [property: JsonPropertyName("videoFileName")] string? VideoFileName,
    [property: JsonPropertyName("videoExtension")] string? VideoExtension,
    [property: JsonPropertyName("mediaType")] string? MediaType,
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
    /// Convert Song to sanitized upload DTO (PRIVACY: excludes file paths).
    /// Use this for uploading to backend - only contains Artist, Title, and metadata fields.
    /// </summary>
    public static SongUploadDto ConvertSongToUploadDto(Song s)
    {
        string? metadataJson = null;

        if (s.MediaType == MediaType.Video)
        {
            // Video songs always include mediaType and extension; add durationSeconds when available
            if (s.DurationSeconds > 0)
            {
                var metadata = new { mediaType = "video", extension = s.VideoExtension, durationSeconds = s.DurationSeconds };
                metadataJson = JsonSerializer.Serialize(metadata);
            }
            else
            {
                var metadata = new { mediaType = "video", extension = s.VideoExtension };
                metadataJson = JsonSerializer.Serialize(metadata);
            }
        }
        else if (s.DurationSeconds > 0)
        {
            // Mp3Cdg songs: only include metadata when duration is known
            var metadata = new { durationSeconds = s.DurationSeconds };
            metadataJson = JsonSerializer.Serialize(metadata);
        }

        return new SongUploadDto(
            Id: s.Id.ToString(),
            Artist: s.Artist,
            Title: s.Title,
            MetadataJson: metadataJson
        );
    }

    /// <summary>
    /// Convert Song to full DTO (includes file paths for internal use).
    /// WARNING: This contains private file paths - use ConvertSongToUploadDto for backend uploads.
    /// </summary>
    public static SongDto ConvertSongToDto(Song s) => new(
        Id: s.Id.ToString(),
        Artist: s.Artist,
        Title: s.Title,
        Mp3FileName: s.Mp3FileName,
        CdgFileName: s.CdgFileName,
        VideoFileName: s.VideoFileName,
        VideoExtension: s.VideoExtension,
        MediaType: s.MediaType == MediaType.Video ? "video" : "mp3cdg",
        Path: s.Path,
        FullPath: s.FullPath,
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
    public static Song ConvertJsonToSong(JsonElement s)
    {
        // Parse metadata to extract MediaType, VideoExtension, and DurationSeconds
        var mediaType = MediaType.Mp3Cdg; // Default for backward compatibility
        string? videoExtension = null;
        int durationSeconds = 0;
        
        if (s.TryGetProperty("metadataJson", out var metadataJsonProp) && 
            metadataJsonProp.ValueKind == JsonValueKind.String)
        {
            var metadataJsonStr = metadataJsonProp.GetString();
            if (!string.IsNullOrWhiteSpace(metadataJsonStr))
            {
                try
                {
                    var metadata = JsonDocument.Parse(metadataJsonStr).RootElement;
                    
                    // Extract mediaType (supports both legacy integer and current string formats)
                    if (metadata.TryGetProperty("mediaType", out var mediaTypeProp))
                    {
                        if (mediaTypeProp.ValueKind == JsonValueKind.String)
                        {
                            var mediaTypeValue = mediaTypeProp.GetString();
                            if (string.Equals(mediaTypeValue, "video", StringComparison.OrdinalIgnoreCase))
                            {
                                mediaType = MediaType.Video;
                            }
                        }
                        else if (mediaTypeProp.ValueKind == JsonValueKind.Number)
                        {
                            var mediaTypeValue = mediaTypeProp.GetInt32();
                            if (Enum.IsDefined(typeof(MediaType), mediaTypeValue))
                            {
                                mediaType = (MediaType)mediaTypeValue;
                            }
                        }
                    }
                    
                    // Extract video extension
                    if (metadata.TryGetProperty("extension", out var extensionProp) && 
                        extensionProp.ValueKind == JsonValueKind.String)
                    {
                        videoExtension = extensionProp.GetString();
                    }

                    // Extract durationSeconds
                    if (metadata.TryGetProperty("durationSeconds", out var durationProp) &&
                        durationProp.ValueKind == JsonValueKind.Number)
                    {
                        durationSeconds = durationProp.GetInt32();
                    }
                }
                catch
                {
                    // Invalid JSON in metadata - use defaults
                }
            }
        }
        
        var artist = s.GetProperty("artist").GetString() ?? string.Empty;
        var title = s.GetProperty("title").GetString() ?? string.Empty;

        return new Song
        {
            Id = Guid.Parse(s.GetProperty("id").GetString()!),
            Artist = artist,
            Title = title,
            MediaType = mediaType,
            // PRIVACY: File paths never returned from backend (empty/null for secondary tabs)
            Mp3FileName = null,
            CdgFileName = null,
            VideoFileName = null,
            VideoExtension = videoExtension,
            Path = null,
            FullPath = null,
            SourceType = SongSourceType.Directory,
            ZipFileName = null,
            ZipEntryMp3Path = null,
            ZipEntryCdgPath = null,
            ZipFilePath = null,
            AddedBySinger = s.TryGetProperty("addedBySinger", out var singer) ? singer.GetString() : null,
            DurationSeconds = durationSeconds
        };
    }

    public static Song ConvertDtoToSong(SongDto dto)
    {
        // Parse MediaType from string (JavaScript sends 'video' or 'mp3cdg')
        var mediaType = MediaType.Mp3Cdg; // Default for backward compatibility
        if (!string.IsNullOrEmpty(dto.MediaType))
        {
            if (dto.MediaType.Equals("video", StringComparison.OrdinalIgnoreCase))
            {
                mediaType = MediaType.Video;
            }
        }

        return new Song
        {
            Id = Guid.Parse(dto.Id),
            Artist = dto.Artist ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            MediaType = mediaType,
            Mp3FileName = dto.Mp3FileName,
            CdgFileName = dto.CdgFileName,
            VideoFileName = dto.VideoFileName,
            VideoExtension = dto.VideoExtension,
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
}
