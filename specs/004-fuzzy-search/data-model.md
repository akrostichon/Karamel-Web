# Data Model: Smart Search — Fuzzy Matching, Relevance Ranking, and Spelling Suggestions

**Phase 1 output for**: `specs/004-fuzzy-search/plan.md`  
**Date**: 2026-03-08

---

## Overview

This feature introduces no new database tables or columns. All new data
structures are:
- **Backend-only** compiled types (`RelevanceTier`, `ScoredSongResult`) used
  solely during a request to compute ordering.
- **API-layer DTOs** (`SearchSuggestionDto`, `LibraryResponseDto`) that cross
  the HTTP and SignalR boundaries.
- **Frontend state extensions** (`Suggestions` on `LibraryState`) that drive
  UI rendering.

---

## Backend: Internal Types

### `RelevanceTier` (C# enum — `Karamel.Backend.Services`)

Classifies a search result hit relative to the user's query.

```csharp
public enum RelevanceTier
{
    ExactTitle   = 0,   // Title equals query (case-insensitive)
    PartialTitle = 1,   // Title contains query as substring
    ArtistOnly   = 2,   // Artist contains query, title does not
    FuzzyMatch   = 3    // Within edit-distance threshold (no substring match)
}
```

**Rules**:
- A song maps to exactly **one** tier per query — the lowest-numbered
  (highest-priority) tier that applies.
- Within each tier, songs are sorted by `Artist ASC, Title ASC` (stable
  secondary sort satisfying the edge case for ties).
- `FuzzyMatch` songs that pass the LIKE phase are never downgraded to
  `FuzzyMatch`; the LIKE phases take precedence.

---

### `ScoredSongResult` (C# record — `Karamel.Backend.Services`, internal)

An intermediate result produced by `IFuzzySearchService` during scoring.

```csharp
internal record ScoredSongResult(
    SongListItemDto Song,
    RelevanceTier   Tier,
    int             EditDistance   // 0 for substring matches; OSA distance for fuzzy matches
);
```

**Not exposed** outside `Karamel.Backend.Services`. Used only to sort
candidates before mapping to the public DTO list.

---

### `IFuzzySearchService` (interface — `Karamel.Backend.Services`)

Stateless service responsible for all fuzzy matching and suggestion logic.

```csharp
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
}
```

**Constants** (defined on the implementation class):
```csharp
public const int MinFuzzyQueryLength = 3;   // FR-003
public const int MaxCandidateForFuzzy = 500; // R3 bounding cap
public const int MaxSuggestionCandidates = 300;
```

**`GenerateSuggestions` normalization formula**:

For each unique word token `t` extracted from any candidate's `Artist` or `Title` field, compute:

```
normalizedDistance = ComputeOsaDistance(t.ToLowerInvariant(), query.ToLowerInvariant())
                     / (double)Math.Max(t.Length, query.Length)
```

Keep tokens where `normalizedDistance ≤ 0.5`. Rank survivors by ascending `normalizedDistance`;
break ties alphabetically. Return the top `maxSuggestions` tokens as `SearchSuggestionDto` records.
Tokenization splits on whitespace and punctuation; tokens shorter than `MinFuzzyQueryLength` (3) are
skipped.

---

## API Layer: DTOs

### `SearchSuggestionDto` (C# record — `Karamel.Backend.Controllers.LibraryDtos`)

Represents a single spelling suggestion returned when zero results are found.

```csharp
public record SearchSuggestionDto(
    [property: JsonPropertyName("text")]        string Text,
    [property: JsonPropertyName("sourceField")] string SourceField   // "title" | "artist"
);
```

**Validation**:
- `Text`: non-empty; max 512 chars (same as `Song.Artist`/`Song.Title` max).
- `SourceField`: "artist" or "title".
- `Suggestions` list: 0–3 items (FR-004 caps at 3).

**JSON example**:
```json
{ "text": "Beyoncé", "sourceField": "artist" }
```

---

### `LibraryResponseDto` (C# record — `Karamel.Backend.Controllers.LibraryDtos`)

Replaces the current pattern of `Ok(result.Items)` + `X-Total-Count` header.

```csharp
public record LibraryResponseDto(
    [property: JsonPropertyName("items")]       IEnumerable<SongListItemDto>      Items,
    [property: JsonPropertyName("totalCount")]  long                              TotalCount,
    [property: JsonPropertyName("page")]        int                               Page,
    [property: JsonPropertyName("pageSize")]    int                               PageSize,
    [property: JsonPropertyName("suggestions")] IReadOnlyList<SearchSuggestionDto> Suggestions
);
```

**Rules**:
- `suggestions` is always present in the response (never null), but the
  list is empty when results are found (FR-006).
- `totalCount` reflects the number of relevance-scored hits, capped at
  `MaxCandidateForFuzzy = 500`.
- The `X-Total-Count` HTTP header is retained (same value as `totalCount`)
  for backward-compatibility with any scripted consumers.

**JSON example (with results)**:
```json
{
  "items": [
    { "id": "...", "sessionId": "...", "artist": "Queen", "title": "Bohemian Rhapsody", "metadataJson": null, "addedAt": "..." }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "suggestions": []
}
```

**JSON example (zero results, with suggestions)**:
```json
{
  "items": [],
  "totalCount": 0,
  "page": 1,
  "pageSize": 50,
  "suggestions": [
    { "text": "Bohemian Rhapsody", "sourceField": "title" }
  ]
}
```

---

## Frontend: State Extension

### `LibraryState` extension (`Karamel.Web.Store.Library`)

Two new properties added to the existing `LibraryState` record:

```csharp
// Spelling suggestions (populated only when Songs is empty and a search is active)
public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();

// Whether the last search produced zero results (drives suggestion display logic)
public bool HasSearchedWithNoResults { get; init; } = false;
```

**Invariant**: `Suggestions.Count > 0` only when `Songs.Count == 0` and
`!string.IsNullOrEmpty(ServerSearchQuery)`. This is enforced by the reducer.

---

### New Fluxor Actions (`Karamel.Web.Store.Library`)

```csharp
// Dispatched by LoadPageSuccessAction handler when suggestions are present
public record SearchSuggestionsReceivedAction(IReadOnlyList<string> Suggestions);

// Dispatched when singer taps a suggestion chip
public record ApplySuggestionAction(string SuggestionText);
```

**`LoadPageSuccessAction`** is extended:

```csharp
// MODIFIED (existing record):
public record LoadPageSuccessAction(
    IReadOnlyList<Song>    Songs,
    int                    Page,
    long                   TotalCount,
    string?                SearchQuery,
    bool                   Append,
    IReadOnlyList<string>  Suggestions   // NEW: empty list when results found
);
```

---

### State Transitions

```
Singer types ≥ 3 chars
  → Dispatcher.Dispatch(LoadPageAction(Page:1, SearchQuery:"rapsody", Append:false))
  → LibraryEffects.HandleLoadPageAction
      → FetchLibraryPageAsync → returns { items:[], totalCount:0, suggestions:["Rhapsody"] }
      → Dispatcher.Dispatch(LoadPageSuccessAction(Songs:[], ..., Suggestions:["Rhapsody"]))
  → LibraryReducer reduces:
      songs = []
      suggestions = ["Rhapsody"]
      hasSearchedWithNoResults = true
  → LibrarySearch.razor renders suggestion chip "Rhapsody"

Singer taps chip "Rhapsody"
  → Dispatcher.Dispatch(ApplySuggestionAction("Rhapsody"))
  → LibraryReducer sets SearchFilter = "Rhapsody"
  → LibrarySearch.razor triggers new search with "Rhapsody"
```

---

## Entity Relationships

```
Session (1) ──── (N) Song           [unchanged]
Song ──────────────── SongListItemDto   [existing DTO, unchanged]

Request scope only (not persisted):
  IFuzzySearchService.ScoreAndSort()
    → ScoredSongResult[]   (Song + Tier + EditDistance)
    → sorted → paged → mapped to LibraryResponseDto.Items

  IFuzzySearchService.GenerateSuggestions()
    → SearchSuggestionDto[]  (text + sourceField)
    → included in LibraryResponseDto.Suggestions
```

---

## Validation Rules

| Entity | Field | Rule |
|---|---|---|
| `SearchSuggestionDto` | `Text` | Non-empty; ≤ 512 chars; deduplicated across the 3 suggestions |
| `SearchSuggestionDto` | `SourceField` | Must be `"artist"` or `"title"` |
| `LibraryResponseDto` | `Suggestions` | 0–3 items; empty when `Items.Count > 0` |
| `LoadPageAction` | `SearchQuery` | Fuzzy logic applied only when length ≥ `MinFuzzyQueryLength` (3) |
| `FuzzySearchService` | candidates cap | Never process more than `MaxCandidateForFuzzy` (500) songs in one scoring pass |
