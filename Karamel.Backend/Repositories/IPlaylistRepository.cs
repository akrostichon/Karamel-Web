using Karamel.Backend.Models;

namespace Karamel.Backend.Repositories
{
    public interface IPlaylistRepository
    {
        Task AddAsync(Playlist playlist);
        Task<Playlist?> GetAsync(Guid id);
        Task<Playlist?> GetBySessionIdAsync(Guid sessionId);
        Task UpdateAsync(Playlist playlist);
        Task DeleteAsync(Guid id);
    }
}
