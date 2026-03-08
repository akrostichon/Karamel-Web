using Karamel.Backend.Controllers;

namespace Karamel.Backend.Services;

/// <summary>
/// Implements OSA (Optimal String Alignment / restricted Damerau-Levenshtein) fuzzy search.
/// Provides relevance-tier classification and spelling-suggestion generation.
/// </summary>
public sealed class FuzzySearchService : IFuzzySearchService
{
    // ────────────────────────────────────────────────────────────────────────
    // ComputeOsaDistance — two-row DP, O(|a|·|b|) time, O(|b|) space
    // ────────────────────────────────────────────────────────────────────────

    public int ComputeOsaDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        if (a == b)        return 0;

        int lenA = a.Length;
        int lenB = b.Length;

        // prev2 = row i-2, prev1 = row i-1, curr = row i
        int[] prev2 = new int[lenB + 1];
        int[] prev1 = new int[lenB + 1];
        int[] curr  = new int[lenB + 1];

        for (int j = 0; j <= lenB; j++) prev1[j] = j;

        for (int i = 1; i <= lenA; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= lenB; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(prev1[j] + 1,        // deletion
                             curr[j - 1] + 1),    // insertion
                    prev1[j - 1] + cost);          // substitution

                // Transposition (OSA): swap a[i-1] ↔ a[i-2] and b[j-1] ↔ b[j-2]
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    curr[j] = Math.Min(curr[j], prev2[j - 2] + cost);
                }
            }
            // Rotate rows
            int[] tmp = prev2;
            prev2 = prev1;
            prev1 = curr;
            curr  = tmp;
        }

        return prev1[lenB];
    }

    // ────────────────────────────────────────────────────────────────────────
    // GetThreshold
    // ────────────────────────────────────────────────────────────────────────

    public int GetThreshold(int queryLength)
    {
        if (queryLength < IFuzzySearchService.MinFuzzyQueryLength) return 0;
        if (queryLength <= 5) return 1;
        return 2;
    }

    // ────────────────────────────────────────────────────────────────────────
    // ScoreAndSort
    // ────────────────────────────────────────────────────────────────────────

    public IReadOnlyList<ScoredSongResult> ScoreAndSort(
        IEnumerable<SongListItemDto> candidates,
        string query)
    {
        query = (query ?? string.Empty).Trim();

        // Empty query — return all candidates alphabetically (bypass scoring)
        if (string.IsNullOrWhiteSpace(query))
        {
            return candidates
                .OrderBy(s => s.Artist, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                .Select(s => new ScoredSongResult(s, RelevanceTier.PartialTitle, 0))
                .ToList();
        }

        var qLower     = query.ToLowerInvariant();
        var threshold  = GetThreshold(query.Length);
        var results    = new List<ScoredSongResult>();

        foreach (var song in candidates)
        {
            var titleLower  = (song.Title  ?? string.Empty).ToLowerInvariant();
            var artistLower = (song.Artist ?? string.Empty).ToLowerInvariant();

            // Tier 0 — ExactTitle
            if (titleLower == qLower)
            {
                results.Add(new ScoredSongResult(song, RelevanceTier.ExactTitle, 0));
                continue;
            }

            // Tier 1 — PartialTitle (title contains query as substring)
            if (titleLower.Contains(qLower))
            {
                results.Add(new ScoredSongResult(song, RelevanceTier.PartialTitle, 0));
                continue;
            }

            // Tier 2 — ArtistOnly (artist contains query, title does not)
            if (artistLower.Contains(qLower))
            {
                results.Add(new ScoredSongResult(song, RelevanceTier.ArtistOnly, 0));
                continue;
            }

            // Tier 3 — FuzzyMatch (only for queries >= MinFuzzyQueryLength)
            if (threshold > 0)
            {
                // Compare against individual words in title and artist for efficiency
                int minDist = int.MaxValue;

                // Check title tokens and full title
                minDist = Math.Min(minDist, ComputeOsaDistance(titleLower, qLower));
                foreach (var token in Tokenize(titleLower))
                {
                    minDist = Math.Min(minDist, ComputeOsaDistance(token, qLower));
                }

                // Check artist tokens and full artist
                minDist = Math.Min(minDist, ComputeOsaDistance(artistLower, qLower));
                foreach (var token in Tokenize(artistLower))
                {
                    minDist = Math.Min(minDist, ComputeOsaDistance(token, qLower));
                }

                if (minDist <= threshold)
                {
                    results.Add(new ScoredSongResult(song, RelevanceTier.FuzzyMatch, minDist));
                }
            }
        }

        // Order: tier ASC, artist ASC, title ASC; within FuzzyMatch tie-break by distance first
        return results
            .OrderBy(r => (int)r.Tier)
            .ThenBy(r => r.EditDistance)
            .ThenBy(r => r.Song.Artist, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Song.Title,  StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ────────────────────────────────────────────────────────────────────────
    // GenerateSuggestions (Phase 5 — US3)
    // ────────────────────────────────────────────────────────────────────────

    public IReadOnlyList<SearchSuggestionDto> GenerateSuggestions(
        IEnumerable<SongListItemDto> candidates,
        string query,
        int maxSuggestions = 3)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < IFuzzySearchService.MinFuzzyQueryLength)
            return Array.Empty<SearchSuggestionDto>();

        var qLower = query.ToLowerInvariant();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<(string Text, string SourceField, double NormalizedDist)>();

        foreach (var song in candidates)
        {
            CollectTokenSuggestions(song.Title,  "title",  qLower, seen, results);
            CollectTokenSuggestions(song.Artist, "artist", qLower, seen, results);
        }

        return results
            .OrderBy(r => r.NormalizedDist)
            .ThenBy(r => r.Text, StringComparer.OrdinalIgnoreCase)
            .Take(maxSuggestions)
            .Select(r => new SearchSuggestionDto(r.Text, r.SourceField))
            .ToList();
    }

    private void CollectTokenSuggestions(
        string? field,
        string sourceField,
        string qLower,
        HashSet<string> seen,
        List<(string Text, string SourceField, double NormalizedDist)> results)
    {
        if (string.IsNullOrWhiteSpace(field)) return;

        foreach (var token in Tokenize(field.ToLowerInvariant()))
        {
            if (seen.Contains(token)) continue;

            var dist = ComputeOsaDistance(token, qLower);
            var normalizedDist = dist / (double)Math.Max(token.Length, qLower.Length);

            if (normalizedDist <= 0.5)
            {
                seen.Add(token);
                results.Add((token, sourceField, normalizedDist));
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static IEnumerable<string> Tokenize(string text) =>
        text.Split(new[] { ' ', '-', '\'', ',', '.', '(', ')', '&', '/' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= IFuzzySearchService.MinFuzzyQueryLength);
}
