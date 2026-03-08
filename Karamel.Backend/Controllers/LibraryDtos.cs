using System.Text.Json.Serialization;

namespace Karamel.Backend.Controllers
{
    public record SongListItemDto(Guid Id, Guid SessionId, string Artist, string Title, string? MetadataJson, DateTime AddedAt);
    public record SongUploadDto(Guid Id, string Artist, string Title, string? MetadataJson);
    public record PagedResult<T>(IEnumerable<T> Items, int Page, int PageSize, long TotalCount)
    {
        public IReadOnlyList<SearchSuggestionDto> Suggestions { get; init; } = Array.Empty<SearchSuggestionDto>();
    }

    public record SearchSuggestionDto(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("sourceField")] string SourceField
    );

    public record LibraryResponseDto(
        [property: JsonPropertyName("items")] IEnumerable<SongListItemDto> Items,
        [property: JsonPropertyName("totalCount")] long TotalCount,
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("pageSize")] int PageSize,
        [property: JsonPropertyName("suggestions")] IReadOnlyList<SearchSuggestionDto> Suggestions
    );
}
