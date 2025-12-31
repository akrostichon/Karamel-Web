using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Karamel.Backend.Data;
using Karamel.Backend.Models;

namespace Karamel.Backend.Repositories
{
    public class PlaylistRepository : IPlaylistRepository
    {
        private readonly BackendDbContext _db;
        public PlaylistRepository(BackendDbContext db) => _db = db;

        public async Task AddAsync(Playlist playlist)
        {
            await _db.Playlists.AddAsync(playlist);
            await _db.SaveChangesAsync();
        }

        public async Task<Playlist?> GetAsync(Guid id)
        {
            return await _db.Playlists.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task UpdateAsync(Playlist playlist)
        {
            _db.Playlists.Update(playlist);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var p = await _db.Playlists.FindAsync(id);
            if (p == null) return;
            _db.Playlists.Remove(p);
            await _db.SaveChangesAsync();
        }
    }
}
