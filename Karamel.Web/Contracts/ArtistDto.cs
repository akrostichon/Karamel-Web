using System.Text.Json.Serialization;

namespace Karamel.Web.Contracts;

public record ArtistDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("songCount")] int SongCount
);
