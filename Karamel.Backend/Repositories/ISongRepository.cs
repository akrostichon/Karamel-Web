using Karamel.Backend.Controllers;

namespace Karamel.Backend.Repositories
{
    public interface ISongRepository
    {
        Task BulkUpsertAsync(Guid sessionId, IEnumerable<SongUploadDto> songs);
        Task<PagedResult<SongListItemDto>> GetPageAsync(Guid sessionId, int page, int pageSize, string? search, string? sort, string? artist = null);
        Task<SongListItemDto?> GetByIdAsync(Guid sessionId, Guid songId);
        Task DeleteBySessionAsync(Guid sessionId);

        /// <summary>
        /// Returns all distinct artists in the session library,
        /// ordered alphabetically, with song counts.
        /// Artists with null or whitespace names are excluded.
        /// </summary>
        Task<IReadOnlyList<ArtistSummaryDto>> GetArtistsAsync(Guid sessionId);
    }
}
