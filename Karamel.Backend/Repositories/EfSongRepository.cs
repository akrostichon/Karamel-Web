using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Karamel.Backend.Controllers;
using Karamel.Backend.Data;
using Karamel.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Karamel.Backend.Repositories
{
    public class EfSongRepository : ISongRepository
    {
        private readonly BackendDbContext _db;

        public EfSongRepository(BackendDbContext db)
        {
            _db = db;
        }

        public async Task BulkUpsertAsync(Guid sessionId, IEnumerable<SongUploadDto> songs)
        {
            if (songs == null) return;

            var list = songs.Where(s => !string.IsNullOrWhiteSpace(s.Artist) || !string.IsNullOrWhiteSpace(s.Title))
                            .Select(s => new { Artist = (s.Artist ?? string.Empty).Trim(), Title = (s.Title ?? string.Empty).Trim(), MetadataJson = s.MetadataJson })
                            .ToList();

            if (!list.Any()) return;

            // Deduplicate uploads by Artist+Title
            var keys = list.Select(s => new { s.Artist, s.Title }).Distinct().ToList();

            // Get existing songs for session
            var existing = await _db.Songs.Where(s => s.SessionId == sessionId)
                                         .ToListAsync();

            var existingKeys = existing.Select(e => new { e.Artist, e.Title }).ToHashSet();

            var toAdd = new List<Song>();
            foreach (var s in keys)
            {
                if (!existingKeys.Contains(new { s.Artist, s.Title }))
                {
                    toAdd.Add(new Song
                    {
                        Id = Guid.NewGuid(),
                        SessionId = sessionId,
                        Artist = s.Artist,
                        Title = s.Title,
                        MetadataJson = list.FirstOrDefault(x => x.Artist == s.Artist && x.Title == s.Title)?.MetadataJson,
                        AddedAt = DateTime.UtcNow
                    });
                }
            }

            if (toAdd.Any())
            {
                await _db.Songs.AddRangeAsync(toAdd);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<PagedResult<SongListItemDto>> GetPageAsync(Guid sessionId, int page, int pageSize, string? search, string? sort)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 50;

            var query = _db.Songs.AsNoTracking().Where(s => s.SessionId == sessionId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                query = query.Where(s => EF.Functions.Like(s.Artist, $"%{q}%") || EF.Functions.Like(s.Title, $"%{q}%"));
            }

            // Sorting: default by Artist then Title, support 'artist' or 'addedAt'
            query = sort switch
            {
                "addedAt" => query.OrderByDescending(s => s.AddedAt),
                "artist" => query.OrderBy(s => s.Artist).ThenBy(s => s.Title),
                _ => query.OrderBy(s => s.Artist).ThenBy(s => s.Title)
            };

            var total = await query.LongCountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new SongListItemDto(s.Id, s.SessionId, s.Artist, s.Title, s.MetadataJson, s.AddedAt))
                .ToListAsync();

            return new PagedResult<SongListItemDto>(items, page, pageSize, total);
        }

        public async Task<SongListItemDto?> GetByIdAsync(Guid sessionId, Guid songId)
        {
            var s = await _db.Songs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == songId && x.SessionId == sessionId);
            if (s == null) return null;
            return new SongListItemDto(s.Id, s.SessionId, s.Artist, s.Title, s.MetadataJson, s.AddedAt);
        }

        public async Task DeleteBySessionAsync(Guid sessionId)
        {
            var list = await _db.Songs.Where(s => s.SessionId == sessionId).ToListAsync();
            if (!list.Any()) return;
            _db.Songs.RemoveRange(list);
            await _db.SaveChangesAsync();
        }
    }
}
