using Karamel.Backend.Controllers;

namespace Karamel.Backend.Repositories
{
    public interface ISongRepository
    {
        Task BulkUpsertAsync(Guid sessionId, IEnumerable<SongUploadDto> songs);
        Task<PagedResult<SongListItemDto>> GetPageAsync(Guid sessionId, int page, int pageSize, string? search, string? sort);
        Task<SongListItemDto?> GetByIdAsync(Guid sessionId, Guid songId);
        Task DeleteBySessionAsync(Guid sessionId);
    }
}
