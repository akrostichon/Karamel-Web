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

        public async Task<Playlist> GetBySessionIdAsync(Guid sessionId)
        {
            // One session = one playlist: Use sessionId as playlistId for simplicity
            var playlist = await _db.Playlists
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == sessionId);
            
            if (playlist == null)
            {
                // Create playlist with Id = sessionId (one-to-one mapping)
                playlist = new Playlist { Id = sessionId, SessionId = sessionId };
                await _db.Playlists.AddAsync(playlist);
                await _db.SaveChangesAsync();
            }
            
            return playlist;
        }

        public async Task UpdateAsync(Playlist playlist)
        {
            // Attach playlist if not tracked
            var tracked = _db.Playlists.Local.FirstOrDefault(p => p.Id == playlist.Id);
            if (tracked == null)
            {
                _db.Playlists.Attach(playlist);
            }

            // Fix N+1 query: Batch fetch all existing item IDs in a single query
            // Performance: 50 items = 51 queries → 2 queries (96% reduction)
            var itemIds = playlist.Items.Select(i => i.Id).ToList();
            var existingItemIds = await _db.PlaylistItems
                .Where(pi => itemIds.Contains(pi.Id))
                .Select(pi => pi.Id)
                .ToListAsync();
            
            // Use HashSet for O(1) lookup instead of repeated database queries
            var existingIds = new HashSet<Guid>(existingItemIds);

            // For each item, ensure new items are added to the context so EF issues INSERTs
            foreach (var item in playlist.Items)
            {
                if (!existingIds.Contains(item.Id))
                {
                    await _db.PlaylistItems.AddAsync(item);
                }
                else
                {
                    _db.PlaylistItems.Update(item);
                }
            }

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
