using Karamel.Backend.Controllers;

namespace Karamel.Backend.Services
{
    public enum RelevanceTier
    {
        ExactTitle   = 0,
        PartialTitle = 1,
        ArtistOnly   = 2,
        FuzzyMatch   = 3
    }

    public record ScoredSongResult(
        SongListItemDto Song,
        RelevanceTier   Tier,
        int             EditDistance
    );

    public interface IFuzzySearchService
    {
        /// <summary>
        /// Score and sort a candidate set against <paramref name="query"/>.
        /// Returns candidates within threshold, ordered by relevance tier then alphabetically.
        /// </summary>
        IReadOnlyList<ScoredSongResult> ScoreAndSort(
            IEnumerable<SongListItemDto> candidates,
            string query);

        /// <summary>
        /// Derive up to <paramref name="maxSuggestions"/> alternative search terms
        /// from <paramref name="candidates"/> when a query returns zero results.
        /// </summary>
        IReadOnlyList<SearchSuggestionDto> GenerateSuggestions(
            IEnumerable<SongListItemDto> candidates,
            string query,
            int maxSuggestions = 3);

        /// <summary>
        /// Compute Optimal String Alignment (restricted Damerau-Levenshtein) distance.
        /// Exposed for unit testing.
        /// </summary>
        int ComputeOsaDistance(string a, string b);

        /// <summary>
        /// Determine the edit-distance threshold for a given query length.
        /// Returns 0 for queries shorter than MinFuzzyQueryLength.
        /// </summary>
        int GetThreshold(int queryLength);

        // Constants
        public const int MinFuzzyQueryLength     = 3;
        public const int MaxCandidateForFuzzy    = 500;
        public const int MaxSuggestionCandidates = 300;
    }
}
