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
        private readonly ILogger<EfSongRepository> _logger;

        public EfSongRepository(BackendDbContext db, ILogger<EfSongRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task BulkUpsertAsync(Guid sessionId, IEnumerable<SongUploadDto> songs)
        {
            if (songs == null) return;

            var list = songs.Where(s => !string.IsNullOrWhiteSpace(s.Artist) || !string.IsNullOrWhiteSpace(s.Title))
                        .Select(s => new { s.Id, Artist = (s.Artist ?? string.Empty).Trim(), Title = (s.Title ?? string.Empty).Trim(), MetadataJson = s.MetadataJson })
                        .ToList();

        if (!list.Any()) return;

        try
        {
            _logger.LogInformation("BulkUpsert: Processing {Count} songs for session {SessionId}", list.Count, sessionId);

            // Use IDs for deduplication (client controls uniqueness)
            var ids = list.Select(s => s.Id).Distinct().ToList();
            _logger.LogDebug("BulkUpsert: {UniqueCount} unique songs after deduplication by ID", ids.Count);

            // Get existing songs for session by ID
            var existing = await _db.Songs.Where(s => s.SessionId == sessionId && ids.Contains(s.Id))
                                         .ToListAsync();
            _logger.LogDebug("BulkUpsert: Found {ExistingCount} existing songs in database", existing.Count);

            var existingIds = existing.Select(e => e.Id).ToHashSet();

            // Update existing songs
            int updatedCount = 0;
            foreach (var existingSong in existing)
            {
                var updated = list.FirstOrDefault(s => s.Id == existingSong.Id);
                if (updated != null)
                {
                    existingSong.Artist = updated.Artist;
                    existingSong.Title = updated.Title;
                    existingSong.MetadataJson = updated.MetadataJson;
                    // Note: AddedAt is NOT updated (preserves original timestamp)
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                _logger.LogInformation("BulkUpsert: Updated {UpdatedCount} existing songs for session {SessionId}", updatedCount, sessionId);
            }

            // Add new songs
            var toAdd = new List<Song>();
            foreach (var s in list)
            {
                if (!existingIds.Contains(s.Id))
                {
                    toAdd.Add(new Song
                    {
                        Id = s.Id,  // Use client-provided ID
                        SessionId = sessionId,
                        Artist = s.Artist,
                        Title = s.Title,
                        MetadataJson = s.MetadataJson,
                        AddedAt = DateTime.UtcNow
                    });
                }
            }

            if (toAdd.Any())
            {
                _logger.LogInformation("BulkUpsert: Adding {NewCount} new songs to database", toAdd.Count);
                await _db.Songs.AddRangeAsync(toAdd);
            }

            if (toAdd.Any() || updatedCount > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("BulkUpsert: Successfully saved {NewCount} new and {UpdatedCount} updated songs for session {SessionId}", toAdd.Count, updatedCount, sessionId);
            }
            else
            {
                _logger.LogInformation("BulkUpsert: No changes (all duplicates) for session {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BulkUpsert failed for session {SessionId}. Attempted to add songs from {TotalCount} input items", sessionId, list.Count);
            throw;
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
