using System.Text.Json.Serialization;
using Karamel.Backend.Models;

namespace Karamel.Backend.Contracts
{
    /// <summary>
    /// DTO representation of session configuration that crosses the wire.
    /// Properties are camel-cased to match JavaScript conventions.
    /// </summary>
    public record SessionConfigDto(
        [property: JsonPropertyName("requireSingerName")] bool RequireSingerName,
        [property: JsonPropertyName("allowSingersToReorder")] bool AllowSingersToReorder,
        [property: JsonPropertyName("pauseBetweenSongsSeconds")] int PauseBetweenSongsSeconds,
        [property: JsonPropertyName("theme")] string? Theme
    )
    {
        /// <summary>
        /// Create a DTO from the domain model.
        /// </summary>
        public static SessionConfigDto FromModel(SessionConfig config) => new(
            config.RequireSingerName,
            config.AllowSingersToReorder,
            config.PauseBetweenSongsSeconds,
            config.Theme
        );

        /// <summary>
        /// Convert this DTO back into a domain model instance.
        /// </summary>
        public SessionConfig ToModel() => new()
        {
            RequireSingerName = RequireSingerName,
            AllowSingersToReorder = AllowSingersToReorder,
            PauseBetweenSongsSeconds = PauseBetweenSongsSeconds,
            Theme = Theme
        };
    }
}
