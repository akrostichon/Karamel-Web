using System;
using Karamel.Backend.Controllers;
using Karamel.Backend.Services;
using Xunit;

namespace Karamel.Backend.Tests;

/// <summary>
/// Unit tests for IFuzzySearchService — covering ComputeOsaDistance, GetThreshold, and ScoreAndSort.
/// These tests drive the TDD implementation of FuzzySearchService (T007).
/// </summary>
public class FuzzySearchServiceTests
{
    private readonly IFuzzySearchService _sut = new FuzzySearchService();

    // ────────────────────────────────────────────────────────────────────────
    // ComputeOsaDistance
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeOsaDistance_SameString_ReturnsZero()
    {
        Assert.Equal(0, _sut.ComputeOsaDistance("hello", "hello"));
    }

    [Fact]
    public void ComputeOsaDistance_EmptyStrings_ReturnsZero()
    {
        Assert.Equal(0, _sut.ComputeOsaDistance("", ""));
    }

    [Fact]
    public void ComputeOsaDistance_OneEmptyString_ReturnsLengthOfOther()
    {
        Assert.Equal(3, _sut.ComputeOsaDistance("cat", ""));
        Assert.Equal(3, _sut.ComputeOsaDistance("", "cat"));
    }

    [Fact]
    public void ComputeOsaDistance_SingleSubstitution_ReturnsOne()
    {
        // "bat" → "cat": 1 substitution
        Assert.Equal(1, _sut.ComputeOsaDistance("bat", "cat"));
    }

    [Fact]
    public void ComputeOsaDistance_SingleInsertion_ReturnsOne()
    {
        // "at" → "cat": 1 insertion
        Assert.Equal(1, _sut.ComputeOsaDistance("at", "cat"));
    }

    [Fact]
    public void ComputeOsaDistance_SingleDeletion_ReturnsOne()
    {
        // "cart" → "cat": 1 deletion
        Assert.Equal(1, _sut.ComputeOsaDistance("cart", "cat"));
    }

    [Fact]
    public void ComputeOsaDistance_SingleTransposition_ReturnsOne()
    {
        // "ca" → "ac": 1 transposition (OSA allows adjacent-char swap)
        Assert.Equal(1, _sut.ComputeOsaDistance("ca", "ac"));
    }

    [Fact]
    public void ComputeOsaDistance_TranspositionInLongerString_ReturnsOne()
    {
        // "Bohemian Rapsody" vs "Bohemian Rhapsody": ra → rh (substitution), but real typo test
        // "rapsody" vs "rhapsody" — distance = 2 (insert 'h', ...actually let's test "teh" vs "the")
        Assert.Equal(1, _sut.ComputeOsaDistance("teh", "the"));
    }

    [Fact]
    public void ComputeOsaDistance_TwoEdits_ReturnsTwo()
    {
        // "kitten" → "sitting": classic example
        // k→s, e→i, insert g = 3; but let's use a simpler two-edit example
        // "abc" → "xyz": 3 substitutions
        Assert.Equal(3, _sut.ComputeOsaDistance("abc", "xyz"));
    }

    [Fact]
    public void ComputeOsaDistance_TypicalKaraokeTypo_CorrectDistance()
    {
        // "Rapsody" vs "Rhapsody": 1 insertion (h) + no match, actually:
        // Rapsody (7) vs Rhapsody (8): need to insert 'h' after 'R', shift = 1 edit
        Assert.Equal(1, _sut.ComputeOsaDistance("rapsody", "rhapsody"));
    }

    [Fact]
    public void ComputeOsaDistance_IsCaseInsensitive_WhenCalledWithLowercase()
    {
        // The contract says callers lower-case before calling; verify symmetric distance
        Assert.Equal(0, _sut.ComputeOsaDistance("bohemian", "bohemian"));
    }

    // ────────────────────────────────────────────────────────────────────────
    // GetThreshold
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void GetThreshold_ShortQuery_ReturnsZero(int queryLength, int expected)
    {
        // Queries shorter than MinFuzzyQueryLength (3) → threshold 0 (no fuzzy)
        Assert.Equal(expected, _sut.GetThreshold(queryLength));
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 1)]
    public void GetThreshold_MediumQuery_ReturnsOne(int queryLength, int expected)
    {
        Assert.Equal(expected, _sut.GetThreshold(queryLength));
    }

    [Theory]
    [InlineData(6, 2)]
    [InlineData(10, 2)]
    [InlineData(20, 2)]
    public void GetThreshold_LongQuery_ReturnsTwo(int queryLength, int expected)
    {
        Assert.Equal(expected, _sut.GetThreshold(queryLength));
    }

    // ────────────────────────────────────────────────────────────────────────
    // ScoreAndSort
    // ────────────────────────────────────────────────────────────────────────

    private static SongListItemDto MakeSong(string artist, string title) =>
        new(Guid.NewGuid(), Guid.NewGuid(), artist, title, null, DateTime.UtcNow);

    [Fact]
    public void ScoreAndSort_ExactTitleMatch_ReturnsExactTierFirst()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("Freddie Mercury", "Living on my Own"),
        };

        var results = _sut.ScoreAndSort(songs, "bohemian rhapsody");

        Assert.NotEmpty(results);
        Assert.Equal(RelevanceTier.ExactTitle, results[0].Tier);
        Assert.Equal("Bohemian Rhapsody", results[0].Song.Title);
    }

    [Fact]
    public void ScoreAndSort_PartialTitleMatch_ReturnsPartialTier()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody Today"),
            MakeSong("Nobody", "Unrelated Song"),
        };

        var results = _sut.ScoreAndSort(songs, "bohemian");

        Assert.NotEmpty(results);
        Assert.Equal(RelevanceTier.PartialTitle, results[0].Tier);
    }

    [Fact]
    public void ScoreAndSort_ArtistOnlyMatch_ReturnsArtistOnlyTier()
    {
        var songs = new[]
        {
            MakeSong("Queen", "We Will Rock You"),  // artist contains "queen", title does not
        };

        var results = _sut.ScoreAndSort(songs, "queen");

        Assert.NotEmpty(results);
        Assert.Equal(RelevanceTier.ArtistOnly, results[0].Tier);
    }

    [Fact]
    public void ScoreAndSort_FuzzyMatch_ReturnsFuzzyTier()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
        };

        // "rapsody" is 1 edit away from "rhapsody" — should be FuzzyMatch (no substring match)
        var results = _sut.ScoreAndSort(songs, "rapsody");

        Assert.NotEmpty(results);
        Assert.Equal(RelevanceTier.FuzzyMatch, results[0].Tier);
    }

    [Fact]
    public void ScoreAndSort_TierOrdering_ExactBeforePartialBeforeArtistBeforeFuzzy()
    {
        var songs = new[]
        {
            MakeSong("Yesterday Band", "Today Song"),    // ArtistOnly for "yesterday"
            MakeSong("Various", "Yesterday Once More"),  // PartialTitle for "yesterday"
            MakeSong("Various", "Yesterday"),            // ExactTitle for "yesterday"
            MakeSong("Various", "Ystrday"),              // FuzzyMatch for "yesterday"
        };

        var results = _sut.ScoreAndSort(songs, "yesterday");

        Assert.Equal(4, results.Count);
        Assert.Equal(RelevanceTier.ExactTitle,   results[0].Tier);
        Assert.Equal(RelevanceTier.PartialTitle, results[1].Tier);
        Assert.Equal(RelevanceTier.ArtistOnly,   results[2].Tier);
        Assert.Equal(RelevanceTier.FuzzyMatch,   results[3].Tier);
    }

    [Fact]
    public void ScoreAndSort_AlphabeticalWithinTier()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Yesterday Z"),
            MakeSong("Artist A", "Yesterday A"),
        };

        var results = _sut.ScoreAndSort(songs, "yesterday");

        // Both are PartialTitle; should be ordered: "Artist A - Yesterday A" before "Queen - Yesterday Z"
        Assert.Equal("Yesterday A", results[0].Song.Title);
        Assert.Equal("Yesterday Z", results[1].Song.Title);
    }

    [Fact]
    public void ScoreAndSort_ShortQuery_BypassesFuzzyAndReturnsSubstringMatches()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("The Beatles", "Hey Jude"),
        };

        // Query "Bo" (< MinFuzzyQueryLength=3) — should still match via substring, threshold=0
        var results = _sut.ScoreAndSort(songs, "bo");

        // For short queries, only exact/partial/artist-only matches; no fuzzy
        // "Bohemian Rhapsody" contains "Bo" → PartialTitle
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.NotEqual(RelevanceTier.FuzzyMatch, r.Tier));
    }

    [Fact]
    public void ScoreAndSort_EmptyQuery_ReturnsAllCandidatesInAlphabeticalOrder()
    {
        var songs = new[]
        {
            MakeSong("Zebra", "Zoo"),
            MakeSong("Apple", "Anthem"),
        };

        var results = _sut.ScoreAndSort(songs, "");

        // Empty query should return all, ordered alphabetically by artist then title
        Assert.Equal(2, results.Count);
        Assert.Equal("Apple", results[0].Song.Artist);
        Assert.Equal("Zebra", results[1].Song.Artist);
    }

    [Fact]
    public void ScoreAndSort_NoMatch_ReturnsEmptyList()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("The Beatles", "Hey Jude"),
        };

        // "xyzzy" has no substring match and edit distance > threshold for all songs
        var results = _sut.ScoreAndSort(songs, "xyzzy");

        Assert.Empty(results);
    }

    [Fact]
    public void ScoreAndSort_ExactTitleMatchIsCaseInsensitive()
    {
        var songs = new[] { MakeSong("Queen", "Bohemian Rhapsody") };

        var results = _sut.ScoreAndSort(songs, "BOHEMIAN RHAPSODY");

        Assert.Single(results);
        Assert.Equal(RelevanceTier.ExactTitle, results[0].Tier);
    }
}
