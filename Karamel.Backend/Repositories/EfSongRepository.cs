using Karamel.Backend.Controllers;
using Karamel.Backend.Data;
using Karamel.Backend.Models;
using Karamel.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Karamel.Backend.Repositories
{
    public class EfSongRepository : ISongRepository
    {
        private readonly BackendDbContext _db;
        private readonly ILogger<EfSongRepository> _logger;
        private readonly IFuzzySearchService _fuzzy;

        public EfSongRepository(BackendDbContext db, ILogger<EfSongRepository> logger, IFuzzySearchService fuzzy)
        {
            _db = db;
            _logger = logger;
            _fuzzy = fuzzy;
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
            _logger.LogDebug("SongRepository.GetPageAsync: sessionId={SessionId}, page={Page}, pageSize={PageSize}, search={Search}",
                sessionId, page, pageSize, search ?? "null");
            
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 50;

            var trimmedSearch = search?.Trim();

            // ── Short/empty query: DB-only pagination (no fuzzy) ─────────────
            bool useFuzzy = !string.IsNullOrWhiteSpace(trimmedSearch)
                            && trimmedSearch.Length >= IFuzzySearchService.MinFuzzyQueryLength;

            if (!useFuzzy)
            {
                var query = _db.Songs.AsNoTracking().Where(s => s.SessionId == sessionId);

                if (!string.IsNullOrWhiteSpace(trimmedSearch))
                {
                    var q = trimmedSearch;
                    query = query.Where(s => EF.Functions.Like(s.Artist, $"%{q}%") || EF.Functions.Like(s.Title, $"%{q}%"));
                }

                query = sort switch
                {
                    "addedAt" => query.OrderByDescending(s => s.AddedAt),
                    _ => query.OrderBy(s => s.Artist).ThenBy(s => s.Title)
                };

                var total = await query.LongCountAsync();
                var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(s => new SongListItemDto(s.Id, s.SessionId, s.Artist, s.Title, s.MetadataJson, s.AddedAt))
                    .ToListAsync();

                _logger.LogInformation("[DIAG] SongRepository.GetPageAsync (no-fuzzy): Returning {ItemCount} items (total={TotalCount})",
                    items.Count, total);

                return new PagedResult<SongListItemDto>(items, page, pageSize, total);
            }

            // ── Two-phase fuzzy strategy ──────────────────────────────────────
            // Phase 1: SQL LIKE fetches all candidates (up to MaxCandidateForFuzzy), no Skip/Take
            _logger.LogDebug("SongRepository: Fuzzy search activated for query {Query}", trimmedSearch);

            // For fuzzy we want a broader pool — also include all songs (LIKE '%q%' or just all)
            // Strategy: fetch up to MaxCandidateForFuzzy songs ordered by Artist/Title; for small
            // libraries this is everything; for large ones we cap to keep memory bounded.
            var allCandidates = await _db.Songs.AsNoTracking()
                .Where(s => s.SessionId == sessionId)
                .OrderBy(s => s.Artist).ThenBy(s => s.Title)
                .Take(IFuzzySearchService.MaxCandidateForFuzzy)
                .Select(s => new SongListItemDto(s.Id, s.SessionId, s.Artist, s.Title, s.MetadataJson, s.AddedAt))
                .ToListAsync();

            _logger.LogDebug("SongRepository: Fuzzy candidate pool size={Count} for session {SessionId}",
                allCandidates.Count, sessionId);

            // Phase 2: ScoreAndSort in memory, then C#-side Skip/Take
            var scored = _fuzzy.ScoreAndSort(allCandidates, trimmedSearch!);

            // Zero-results branch: generate spelling suggestions from first-char filtered candidates
            if (scored.Count == 0)
            {
                var firstChar = trimmedSearch![0].ToString();
                var suggestionCandidates = await _db.Songs.AsNoTracking()
                    .Where(s => s.SessionId == sessionId)
                    .Where(s => EF.Functions.Like(s.Artist, $"%{firstChar}%")
                             || EF.Functions.Like(s.Title,  $"%{firstChar}%"))
                    .OrderBy(s => s.Artist)
                    .Take(IFuzzySearchService.MaxSuggestionCandidates)
                    .Select(s => new SongListItemDto(s.Id, s.SessionId, s.Artist, s.Title, s.MetadataJson, s.AddedAt))
                    .ToListAsync();

                var suggestions = _fuzzy.GenerateSuggestions(suggestionCandidates, trimmedSearch!);

                _logger.LogDebug("SongRepository: Zero results for query, generated {Count} suggestion(s)", suggestions.Count);

                return new PagedResult<SongListItemDto>(new List<SongListItemDto>(), page, pageSize, 0)
                {
                    Suggestions = suggestions
                };
            }

            var pagedScored = scored
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => r.Song)
                .ToList();

            long totalFuzzy = scored.Count;

            _logger.LogInformation("[DIAG] SongRepository.GetPageAsync (fuzzy): Returning {ItemCount} items (total={TotalCount}) for session {SessionId}",
                pagedScored.Count, totalFuzzy, sessionId);

            return new PagedResult<SongListItemDto>(pagedScored, page, pageSize, totalFuzzy);
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

        public async Task<IReadOnlyList<ArtistSummaryDto>> GetArtistsAsync(Guid sessionId)
        {
            _logger.LogInformation("GetArtists called for session {SessionId}", sessionId);
            try
            {
                var grouped = await _db.Songs
                    .AsNoTracking()
                    .Where(s => s.SessionId == sessionId && s.Artist != null && s.Artist != string.Empty)
                    .GroupBy(s => s.Artist)
                    .Select(g => new ArtistSummaryDto(g.Key!, g.Count()))
                    .ToListAsync();

                return grouped
                    .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArtists failed for session {SessionId}", sessionId);
                throw;
            }
        }
    }
}
