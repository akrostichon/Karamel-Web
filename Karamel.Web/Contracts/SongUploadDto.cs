using System.Text.Json.Serialization;

namespace Karamel.Web.Contracts;

/// <summary>
/// Sanitized DTO for uploading song metadata to backend.
/// PRIVACY: Does NOT include file paths - only Artist, Title, and future metadata.
/// </summary>
public record SongUploadDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("metadataJson")] string? MetadataJson  // Future: duration, genre, album, year
);
