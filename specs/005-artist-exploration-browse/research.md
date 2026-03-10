# Research: Artist Exploration — Browse Mode for LibrarySearch

*Phase 0 output — all NEEDS CLARIFICATION items resolved before implementation begins.*

---

## 1. EF Core GROUP BY with SELECT + ORDER BY (SQLite + SQL Server)

**Question**: Can a `GroupBy → Select(count) → OrderBy` chain be translated to SQL by EF Core 10 for both SQLite and SQL Server providers?

**Finding**: Yes. EF Core 8+ translates `GroupBy(s => s.Artist).Select(g => new { g.Key, Count = g.Count() }).OrderBy(...)` to a single `SELECT Artist, COUNT(*) FROM Songs WHERE ... GROUP BY Artist ORDER BY Artist` query for both providers. There are no `client-side evaluation` warnings for this pattern.

**Decision**: Use LINQ `GroupBy` in `EfSongRepository.GetArtistsAsync`. No raw SQL needed.

**Alternatives considered**:
- Raw SQL `GROUP BY` — rejected: would require provider-specific SQL and bypass EF Core's change-tracking pipeline.
- In-memory grouping after a full table scan — rejected: O(N) memory and unnecessary data transfer for 3,000 songs.

---

## 2. REST vs SignalR for the Artists Endpoint

**Question**: Should `GET /artists` be a REST endpoint or a SignalR hub method (like `GetLibraryPage`)?

**Finding**: `GetLibraryPage` is exposed via SignalR RPC because it is called frequently (every search keystroke and every page load). The artist list is fetched once per session and cached. Real-time updates to the artist list are not required — artists change only when a new library scan completes, at which point the list is invalidated and re-fetched.

**Decision**: REST endpoint only (`GET /api/sessions/{sessionId}/library/artists`). No SignalR hub method needed.

**Alternatives considered**:
- SignalR hub method `GetArtists(sessionId)` — rejected: adds complexity without benefit; `ISessionApiClient` already has an HTTP client for REST calls, and a one-off fetch does not justify a hub method.

---

## 3. Client-Side Artist List Caching Strategy

**Question**: Should the artist list be re-fetched on every mount of `LibrarySearch`, or cached in `LibraryState`?

**Finding**: The artist list for 3,000 songs (~100–200 artists) is a small payload (~5–10 KB). The content is stable within a session; it only changes when a new library scan uploads songs. Caching in `LibraryState` avoids redundant network calls and provides instant display when the user returns to the Library tab after browsing Up Next.

**Decision**: Cache in `LibraryState` with `Artists`, `IsLoadingArtists`, `ArtistsLoaded` fields. Invalidate (`ArtistsLoaded = false`) when a new library scan starts (`ScanProgressAction` with `Complete = false`). This triggers a fresh fetch the next time browse mode is entered.

**Alternatives considered**:
- Re-fetch on every mount — rejected: causes visible loading delay every time the user switches tabs.
- Cache in component local state — rejected: lost on component unmount (tab switch to Up Next and back).

---

## 4. Artist Name Grouping (Case Sensitivity)

**Question**: Should artist names be grouped case-insensitively (e.g., "abba" and "ABBA" merged)?

**Finding**: Songs are uploaded from the local library with the artist name as stored in the ID3 tags. If "abba" and "ABBA" appear in the library, they reflect different tag values and should remain separate entries. The SQL `GROUP BY` uses the database collation — SQL Server (Azure SQL, CI_AS) is case-insensitive by default; SQLite default collation is case-sensitive. This means development (SQLite) and production (SQL Server) may produce different grouping results.

**Decision**: Accept the collation-dependent behavior. Apply `.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)` in **C#** after the query to ensure consistent alphabetical presentation regardless of DB provider. Do not attempt to normalize casing at group time.

**Alternatives considered**:
- `ToLower`/`ToUpper` before grouping — rejected: would corrupt artist names displayed in the UI (e.g., "AC/DC" → "ac/dc").
- Provider-specific collation hints — rejected: adds migration complexity for a cosmetic concern.

---

## 5. Trigger for LoadArtistsAction

**Question**: When exactly should `LoadArtistsAction` be dispatched?

**Finding**: The artist browse mode is shown when `SearchFilter == ""`. This state can be entered in three ways:
1. **Initial load** — `LibrarySearch` mounts with an empty search field and `ScanComplete = true`.
2. **User clears input** — user deletes text or taps ✕; `FilterSongsAction("")` is dispatched.
3. **Tab switch** — user returns to Library tab from Up Next; `LibrarySearch` re-renders with current state.

In all cases, the decision whether to fetch is: `ScanComplete && !ArtistsLoaded && !IsLoadingArtists`.

**Decision**: Add a `TryLoadArtistsIfNeeded()` private helper in `LibrarySearch.razor` that checks these conditions and dispatches `LoadArtistsAction`. Call it:
- In a `LibraryState.StateChanged` subscription handler (covers initial mount + scan completion).
- At the end of `ClearFilter()` and when `OnSearchInput` produces an empty string (covers user-cleared input).

**Alternatives considered**:
- Dispatch from `OnInitializedAsync` only — rejected: misses the case where `ScanComplete` becomes true *after* mount.
- Dispatch from an Effect triggered by `ScanProgressAction(Complete: true)` — rejected: the Effect would not know whether the user is currently in browse mode; it would always load artists even when a search is active.

---

## 6. ArtistsLoaded Invalidation on Session Change

**Question**: What happens if the user creates a new session (navigates back to Home, creates a new session)?

**Finding**: On session change, `LibraryState` is reset through the existing `ResetPaginationAction` and `LoadLibraryAction` pathways (dispatched by `SessionEffects` upon a new session). The artist list must be cleared at the same time.

**Decision**: Handle `ResetPaginationAction` in the `LibraryReducers` to also clear `Artists`, `ArtistsLoaded`, and `IsLoadingArtists`. This piggybacks on the existing session reset mechanism without requiring a new action.

---

## 7. Blank / Null Artist Names

**Question**: What should happen to songs with blank or null artist names in the artist list?

**Finding**: `BulkUpsertAsync` in `EfSongRepository` already trims and accepts songs with empty artist names (karaoke libraries sometimes have untagged files). These songs would appear as an empty-string entry in the artist list.

**Decision**: Exclude songs with null or whitespace-only artist names from the artist list (`WHERE Artist IS NOT NULL AND Artist != ''`). Songs without an artist are findable via title search in the existing search mode; they do not need to appear in the browse list.

---

## Summary

All NEEDS CLARIFICATION items are resolved. No blockers exist for Phase 1.

| Item | Decision |
|------|----------|
| EF Core GroupBy | LINQ `GroupBy` + C#-side `OrderBy(OrdinalIgnoreCase)` |
| REST vs SignalR | REST only |
| Client caching | `LibraryState.Artists` + `ArtistsLoaded` flag |
| Case sensitivity | Accept DB collation; sort in C# |
| Load trigger | `TryLoadArtistsIfNeeded()` helper + StateChanged subscription |
| Session change | `ResetPaginationAction` reducer clears artist list |
| Blank artists | Excluded from group query |
