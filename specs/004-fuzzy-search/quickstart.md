# Quickstart: Smart Search — Fuzzy Matching, Relevance Ranking, and Spelling Suggestions

**Feature branch**: `004-fuzzy-search`  
**Date**: 2026-03-08

This guide provides the end-to-end implementation path for a developer
picking up this feature from scratch. Read `research.md` for the rationale
behind each decision.

---

## Prerequisites

```powershell
# From repo root
git checkout -b feature/fuzzy-search-implementation
dotnet build                      # Must be clean (zero warnings)
dotnet test Karamel.Web.Tests     # Establish passing baseline (≥ 101 tests)
cd Karamel.Web\wwwroot
npm run test:run                  # Establish passing JS baseline
cd ..\..
```

---

## Step 1 — Backend: `FuzzySearchService`

Create `Karamel.Backend/Services/IFuzzySearchService.cs` (interface) and
`FuzzySearchService.cs` (implementation).

### Algorithm skeleton

```csharp
// IFuzzySearchService.cs — in Karamel.Backend.Services namespace
public interface IFuzzySearchService
{
    IReadOnlyList<ScoredSongResult> ScoreAndSort(
        IEnumerable<SongListItemDto> candidates, string query);

    IReadOnlyList<SearchSuggestionDto> GenerateSuggestions(
        IEnumerable<SongListItemDto> candidates, string query, int maxSuggestions = 3);

    int ComputeOsaDistance(string a, string b);
    int GetThreshold(int queryLength);
}
```

```csharp
// FuzzySearchService.cs — key implementation notes
public const int MinFuzzyQueryLength    = 3;
public const int MaxCandidateForFuzzy   = 500;
public const int MaxSuggestionCandidates = 300;

// GetThreshold: returns 0 if queryLength < MinFuzzyQueryLength;
//               1 for lengths 3–5; 2 for lengths ≥ 6
// ComputeOsaDistance: two-row DP with transposition check (OSA algorithm)
// ScoreAndSort: classify each candidate into RelevanceTier, filter by threshold,
//               order by (Tier asc, Artist asc, Title asc)
// GenerateSuggestions: tokenise Artist+Title, compute OSA per token vs query,
//                      keep tokens with normalized distance ≤ 0.5, top 3
```

Register in `Program.cs`:

```csharp
builder.Services.AddSingleton<IFuzzySearchService, FuzzySearchService>();
```

---

## Step 2 — Backend: Update `EfSongRepository.GetPageAsync`

Replace the inline `EF.Functions.Like` logic with a two-phase strategy:

```
Phase 1: SQL LIKE (all sessions pages)
  → If search not set: DB pagination unchanged
  → If search set: fetch ALL LIKE matches (no Skip/Take at DB level), cap at MaxCandidateForFuzzy
    → Score with FuzzySearchService.ScoreAndSort()
    → Apply Skip/Take in C# after scoring
    → If LIKE results = 0: re-query with first-character prefix filter
      → Score again
      → If still 0: fetch MaxSuggestionCandidates songs → GenerateSuggestions()
```

Update `ISongRepository` to inject `IFuzzySearchService` into
`EfSongRepository` (via constructor injection).

Update `PagedResult<T>` in `LibraryDtos.cs` to include `Suggestions`.

---

## Step 3 — Backend: Update `LibraryController`

```csharp
// Before
Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
return Ok(result.Items);

// After
Response.Headers["X-Total-Count"] = result.TotalCount.ToString(); // kept for compat
return Ok(new LibraryResponseDto(
    result.Items, result.TotalCount, page, pageSize, result.Suggestions));
```

---

## Step 4 — Backend: Update `PlaylistHub.GetLibraryPage`

```csharp
// Before
return new { items = result.Items, page = result.Page, pageSize = result.PageSize, totalCount = result.TotalCount };

// After
return new {
    items       = result.Items,
    page        = result.Page,
    pageSize    = result.PageSize,
    totalCount  = result.TotalCount,
    suggestions = result.Suggestions
};
```

---

## Step 5 — JavaScript: Update `signalRBridge.js`

In the REST fallback section of `fetchLibraryPage`, change body parsing:

```javascript
// Before
const items = await resp.json();
const total = parseInt(resp.headers.get('X-Total-Count') || '0');
return { items, page, pageSize, totalCount: total };

// After
const data = await resp.json();
const items = data.items ?? [];
const total = data.totalCount ?? parseInt(resp.headers.get('X-Total-Count') || '0');
const suggestions = (data.suggestions ?? []).map(s => s.text);
return { items, page, pageSize, totalCount: total, suggestions };
```

In the SignalR path, `suggestions` already passes through from the hub
return value — just add it to the return object:

```javascript
// After SignalR invoke
const suggestions = (res.suggestions ?? []).map(s => s.text);
return { ...res, suggestions };
```

---

## Step 6 — Frontend State: `LibraryActions.cs`, `LibraryState.cs`, `LibraryReducers.cs`

### Actions

```csharp
// Add to LibraryActions.cs
public record ApplySuggestionAction(string SuggestionText);

// Modify LoadPageSuccessAction (add Suggestions parameter)
public record LoadPageSuccessAction(
    IReadOnlyList<Song>   Songs,
    int                   Page,
    long                  TotalCount,
    string?               SearchQuery,
    bool                  Append,
    IReadOnlyList<string> Suggestions);   // NEW
```

### State

```csharp
// Add to LibraryState.cs
public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
public bool HasSearchedWithNoResults { get; init; } = false;
```

### Reducer (new cases)

```csharp
// On LoadPageSuccessAction:
//   - Set Suggestions = action.Suggestions
//   - Set HasSearchedWithNoResults = !action.Songs.Any() && action.SearchQuery != null

// On ApplySuggestionAction:
//   - Set SearchFilter = action.SuggestionText
//   - Set Suggestions = []
//   - Set HasSearchedWithNoResults = false
//   - (LibrarySearch.razor will pick up the new SearchFilter and dispatch LoadPageAction)
```

---

## Step 7 — Frontend Component: `LibrarySearch.razor`

Add suggestion chips below the zero-results message:

```razor
@* Inside the "no results" branch, after the alert *@
@if (LibraryState.Value.HasSearchedWithNoResults
     && LibraryState.Value.Suggestions.Any())
{
    <div class="search-suggestions mt-2" role="region" aria-label="Spelling suggestions">
        <span class="text-muted small">Did you mean:</span>
        @foreach (var suggestion in LibraryState.Value.Suggestions)
        {
            <button type="button"
                    class="btn btn-outline-secondary btn-sm ms-2 suggestion-chip"
                    @onclick="() => ApplySuggestion(suggestion)">
                @suggestion
            </button>
        }
    </div>
}
```

Add the handler in `@code`:

```csharp
private void ApplySuggestion(string text)
{
    Dispatcher.Dispatch(new ApplySuggestionAction(text));
    // The reducer updates SearchFilter → triggers OnSearchInput → dispatches LoadPageAction
    Dispatcher.Dispatch(new LoadPageAction(Page: 1, SearchQuery: text, Append: false));
}
```

---

## Step 8 — `LibraryEffects.cs`: Read Suggestions from Response

In `HandleLoadPageAction`, after parsing `songs`, also parse `suggestions`:

```csharp
// After calling TryParseSongsFromResponse:
var suggestions = ParseSuggestions(pageResult);

// In the dispatch:
dispatcher.Dispatch(new LoadPageSuccessAction(
    Songs: songs,
    Page: action.Page,
    TotalCount: totalCount,
    SearchQuery: action.SearchQuery,
    Append: action.Append,
    Suggestions: suggestions     // NEW
));
```

Add helper:

```csharp
private static IReadOnlyList<string> ParseSuggestions(JsonElement response)
{
    if (!response.TryGetProperty("suggestions", out var suggestionsElement) ||
        suggestionsElement.ValueKind != JsonValueKind.Array)
        return Array.Empty<string>();

    return suggestionsElement
        .EnumerateArray()
        .Select(s => s.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty)
        .Where(t => !string.IsNullOrEmpty(t))
        .ToList()
        .AsReadOnly();
}
```

---

## Step 9 — Tests

### Backend (`FuzzySearchServiceTests.cs`)
- OSA distance: basic cases, transpositions, empty strings, identical strings.
- `GetThreshold`: boundary at lengths 3, 5, 6.
- `ScoreAndSort`: exact title hit, partial title hit, artist-only hit, fuzzy hit.
- `GenerateSuggestions`: returns max 3; empty when no close match; empty when results found.

### Backend Integration (`LibraryApiTests.cs`)
- Typo query returns fuzzy match.
- Zero-results query returns suggestions array.
- Relevance ordering: exact title before partial title.
- Suggestions empty when items are non-empty.
- `X-Total-Count` header still present.

### Frontend C# (`LibrarySearchTests.cs`)
- Renders suggestion chips when `HasSearchedWithNoResults = true` and `Suggestions.Count > 0`.
- No chips when results are present.
- Tapping chip dispatches `ApplySuggestionAction` and `LoadPageAction`.

### JavaScript (`signalRBridge.test.js`)
- REST fallback passes `suggestions` array through.
- SignalR path passes `suggestions` array through.
- Empty suggestions when response has no `suggestions` field (backward compat).

---

## Step 10 — Build and Verify

```powershell
dotnet build                      # Must be zero warnings
dotnet test Karamel.Web.Tests     # Must pass ≥ 101 tests
cd Karamel.Web\wwwroot
npm run test:run                  # Must be zero failures
cd ..\..
# Ask user to run backend integration tests:
# dotnet test .\Karamel.Backend.Tests\ -v minimal
```
