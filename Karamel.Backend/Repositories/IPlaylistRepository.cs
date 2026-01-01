using Karamel.Backend.Models;

namespace Karamel.Backend.Repositories
{
    public interface IPlaylistRepository
    {
        Task AddAsync(Playlist playlist);
        Task<Playlist?> GetAsync(Guid id);
        Task UpdateAsync(Playlist playlist);
        Task DeleteAsync(Guid id);
    }
}
