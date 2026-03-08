# Tasks: Smart Search — Fuzzy Matching, Relevance Ranking, and Spelling Suggestions

**Input**: Design documents from `/specs/004-fuzzy-search/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/library-api.md ✅, quickstart.md ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story this task belongs to ([US1], [US2], [US3])
- Exact file paths are given in each task description

---

## Phase 1: Setup

**Purpose**: Establish a clean, verified baseline before any implementation begins.

- [X] T001 Checkout feature branch `004-fuzzy-search`, run `dotnet build` (zero warnings), `dotnet test Karamel.Web.Tests` (≥ 260 pass), and `cd Karamel.Web\wwwroot; npm run test:run` (zero failures), then return to repo root

**Checkpoint**: Clean baseline confirmed — implementation may begin.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types, DTOs, and frontend state extensions that ALL three user stories depend on. Must be complete before US1 implementation begins.

⚠️ **CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T002 Add `SearchSuggestionDto` record (with `[JsonPropertyName]` attributes), add `LibraryResponseDto` record, and extend `PagedResult<T>` with an `IReadOnlyList<SearchSuggestionDto> Suggestions` property in `Karamel.Backend/Controllers/LibraryDtos.cs`
- [X] T003 [P] Create `Karamel.Backend/Services/IFuzzySearchService.cs` — define `RelevanceTier` enum (`ExactTitle=0`, `PartialTitle=1`, `ArtistOnly=2`, `FuzzyMatch=3`), `ScoredSongResult` internal record, and `IFuzzySearchService` interface with `ScoreAndSort`, `GenerateSuggestions`, `ComputeOsaDistance`, and `GetThreshold` method signatures (plus constants `MinFuzzyQueryLength=3`, `MaxCandidateForFuzzy=500`, `MaxSuggestionCandidates=300`)
- [X] T004 [P] Extend `LibraryState` record with `IReadOnlyList<string> Suggestions` (default `Array.Empty<string>()`) and `bool HasSearchedWithNoResults` (default `false`) in `Karamel.Web/Store/Library/LibraryState.cs`
- [X] T005 [P] Add `SearchSuggestionsAction` record (carrying `IReadOnlyList<string> Suggestions`) to `Karamel.Web/Store/Library/LibraryActions.cs`
- [X] T006 Add reducer for `SearchSuggestionsAction` that sets `Suggestions` and `HasSearchedWithNoResults = Suggestions.Count > 0` in `Karamel.Web/Store/Library/LibraryReducers.cs`; additionally reset both properties to defaults on `ResetPaginationAction` (used when the user navigates away or clears the search — this is the correct reset trigger; `SearchSuggestionsAction([])` dispatched by LibraryEffects already handles the results-found case automatically) (depends on T004, T005)

**Checkpoint**: All shared types and state extensions in place — US1, US2, US3 implementation can now begin (in priority order).

---

## Phase 3: User Story 1 — Singer Finds Song Despite Typos (Priority: P1) 🎯 MVP

**Goal**: Implement OSA fuzzy matching so queries with 1–2 character typos return the intended song. Includes the breaking response-format change (`items` object body) required by all downstream phases.

**Independent Test**: Type "Bohemian Rapsody" in the search box → "Bohemian Rhapsody" by Queen appears in results.

### Tests for User Story 1

> Write these FIRST and confirm they FAIL before writing production code.

- [X] T007 [P] [US1] Write unit tests covering `ComputeOsaDistance` (single-char substitution, insertion, deletion, transposition), `GetThreshold` (returns 0 for < 3 chars, 1 for 3–5, 2 for ≥ 6), and `ScoreAndSort` (exact title, partial title, artist-only, fuzzy, short-query fallback) in `Karamel.Backend.Tests/FuzzySearchServiceTests.cs`
- [X] T008 [P] [US1] Add integration test cases to `Karamel.Backend.Tests/LibraryApiTests.cs` — seeded library, typo query returns expected song with HTTP 200 and response body as `LibraryResponseDto` object (not plain array)

### Implementation for User Story 1

- [X] T009 [US1] Create `Karamel.Backend/Services/FuzzySearchService.cs` — implement `ComputeOsaDistance` (two-row DP, OSA), `GetThreshold`, and `ScoreAndSort` (classify all four `RelevanceTier` values; filter by threshold; order by `(Tier ASC, Artist ASC, Title ASC)`) — make T007 pass
- [X] T010 [US1] Register `IFuzzySearchService` as singleton in `Karamel.Backend/Program.cs` (add `builder.Services.AddSingleton<IFuzzySearchService, FuzzySearchService>()`)
- [X] T011 [US1] Inject `IFuzzySearchService` into `EfSongRepository` and rewrite `GetPageAsync` two-phase strategy in `Karamel.Backend/Repositories/EfSongRepository.cs` — Phase 1: SQL LIKE fetches all candidates (no Skip/Take at DB); Phase 2: `ScoreAndSort` then C#-side Skip/Take; short/empty queries bypass fuzzy and use DB pagination unchanged — make T008 pass
- [X] T012 [US1] Update `LibraryController.GetPage` to return `Ok(new LibraryResponseDto(...))` and retain `X-Total-Count` header in `Karamel.Backend/Controllers/LibraryController.cs` (depends on T002, T011)
- [X] T013 [US1] Migrate existing response-body assertions in `Karamel.Backend.Tests/LibraryApiTests.cs` from reading a JSON array directly to reading the `items` property on the response object (breaking-change migration for T012)
- [X] T014 [P] [US1] Update `PlaylistHub.GetLibraryPage` return value to include `suggestions = result.Suggestions` in `Karamel.Backend/Hubs/PlaylistHub.cs` (depends on T011; initially returns empty list)
- [X] T015 [P] [US1] Update REST fallback in `Karamel.Web/wwwroot/js/signalRBridge.js` — parse `data.items`, `data.totalCount`, and `data.suggestions` from the response object body; map suggestions to string array via `.map(s => s.text)`; return `{ items, page, pageSize, totalCount, suggestions }` (depends on T012 response format)
- [X] T016 [US1] Update `LibraryEffects.cs` to extract `suggestions` array from `JsonElement` using `TryGetProperty("suggestions", ...)` and dispatch `SearchSuggestionsAction` in `Karamel.Web/Store/Library/LibraryEffects.cs` (depends on T005, T012, T015)

**Checkpoint**: Typo-tolerant search is live end-to-end. Run `dotnet test Karamel.Backend.Tests` (T007/T008 green) and `npm run test:run` (T015 no regressions).

---

## Phase 4: User Story 2 — Most Relevant Songs Appear First (Priority: P2)

**Goal**: Validate and complete relevance ordering — ExactTitle first, then PartialTitle, ArtistOnly, FuzzyMatch — preserved across all "Load More" pages. Apply ordering to `SearchLibrary` SignalR RPC.

**Independent Test**: Search "Yesterday" in a library with an exact title match, a partial match, and an artist-only match → exact title appears first.

### Tests for User Story 2

> Write these FIRST and confirm they FAIL before implementation.

- [X] T017 [P] [US2] Add ordering-assertion test cases to `Karamel.Backend.Tests/LibraryApiTests.cs` — assert `ExactTitle` result index < `PartialTitle` index < `ArtistOnly` index < `FuzzyMatch` index; assert alphabetical secondary sort within each tier
- [X] T018 [P] [US2] Add cross-page relevance ordering integration tests to `Karamel.Backend.Tests/LibraryApiTests.cs` — seed 10+ songs with known tiers (ExactTitle, PartialTitle, ArtistOnly, FuzzyMatch), query with page 1 then page 2 (pageSize 3), assert that the `RelevanceTier` sequence is monotone non-decreasing across both pages (i.e. page 2 never contains a higher-priority tier than the last result of page 1)

### Implementation for User Story 2

- [X] T019 [US2] Update `PlaylistHub.SearchLibrary` to pass candidates through `IFuzzySearchService.ScoreAndSort` before returning results in `Karamel.Backend/Hubs/PlaylistHub.cs` (depends on T009; make T017 pass)

**Checkpoint**: All search surfaces return relevance-ordered results. Run `dotnet test Karamel.Backend.Tests` and `dotnet test Karamel.Web.Tests`.

---

## Phase 5: User Story 3 — Singer Gets Spelling Suggestions When No Results Found (Priority: P3)

**Goal**: When a query returns zero results, up to 3 "Did you mean?" suggestions are returned by the backend and rendered in `LibrarySearch.razor`. Tapping a suggestion triggers a new search.

**Independent Test**: Type "Beyonsay" → 0 results → "Did you mean: Beyoncé?" suggestion appears; tapping it fills the search box and re-triggers search.

### Tests for User Story 3

> Write these FIRST and confirm they FAIL before implementation.

- [X] T020 [P] [US3] Add zero-results suggestions test cases to `Karamel.Backend.Tests/LibraryApiTests.cs` — seeded library, heavily-misspelled query returns `items: []` and `suggestions` with ≥ 1 entry; exact/fuzzy-match query returns `suggestions: []`
- [X] T021 [P] [US3] Add suggestion-rendering and tap-to-search component tests to `Karamel.Web.Tests/LibrarySearchTests.cs` — when `LibraryState.Suggestions` is non-empty and `Songs` is empty, "Did you mean?" list renders; clicking a suggestion item dispatches search action with suggestion text

### Implementation for User Story 3

- [X] T022 [US3] Implement `FuzzySearchService.GenerateSuggestions` in `Karamel.Backend/Services/FuzzySearchService.cs` — tokenize `Artist` and `Title` fields (split on whitespace/punctuation, skip tokens shorter than `MinFuzzyQueryLength`); for each token compute `normalizedDistance = ComputeOsaDistance(token.ToLowerInvariant(), query.ToLowerInvariant()) / (double)Math.Max(token.Length, query.Length)`; keep tokens where `normalizedDistance ≤ 0.5`; deduplicate case-insensitively; rank by ascending `normalizedDistance` (alphabetical tie-break); return top `maxSuggestions` (default 3) as `SearchSuggestionDto` list with `SourceField` set to `"artist"` or `"title"` accordingly (make T020 pass)
- [X] T023 [US3] Update `EfSongRepository.GetPageAsync` in `Karamel.Backend/Repositories/EfSongRepository.cs` — add zero-results branch: fetch up to `MaxSuggestionCandidates` songs using first-character prefix filter, pass to `GenerateSuggestions`, populate `result.Suggestions`
- [X] T024 [US3] Update `Karamel.Web/Components/LibrarySearch.razor` — render "Did you mean?" section when `LibraryState.HasSearchedWithNoResults && Suggestions.Count > 0`; each suggestion is a clickable element that populates the search input and triggers a new search dispatch; hide suggestions when results are present or search is cleared (make T021 pass)

**Checkpoint**: Full P3 story is live. Run `dotnet test` (all suites) and `npm run test:run`.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Serialization safety, log-level compliance, and final validation.

- [X] T025 [P] Add round-trip serialization test for `SearchSuggestionDto` and `LibraryResponseDto` (JSON serialize → deserialize → assert all properties preserved) in `Karamel.Backend.Tests/LibraryApiTests.cs`
- [X] T026 [P] Verify all new C# log statements use structured parameters (`_logger.LogInformation("Search {Query} returned {Count}", q, n)`); confirm query text appears at Debug level only (not Info/Warning); confirm no file paths or tokens in any new log output — review `FuzzySearchService.cs`, `EfSongRepository.cs`, `LibraryController.cs`
- [X] T027 [P] Add Vitest tests for `signalRBridge.js` REST fallback — assert `suggestions` is extracted and mapped to strings, assert `items` is read from object body (not as array) in `Karamel.Web/wwwroot/js/signalRBridge.test.js`
- [X] T028 Run full baseline validation: `dotnet build` (zero warnings), `dotnet test Karamel.Web.Tests` (≥ 260 pass, 9 skip), `dotnet test Karamel.Backend.Tests`, `cd Karamel.Web\wwwroot; npm run test:run` (zero failures)

**Checkpoint**: All quality gates pass. Feature branch is ready for code review.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user stories**
- **Phase 3 (US1)**: Depends on Phase 2 — delivers the MVP increment
- **Phase 4 (US2)**: Depends on Phase 3 (ScoreAndSort must exist) — adds ordering validation
- **Phase 5 (US3)**: Depends on Phase 3 (two-phase repo strategy must exist) — adds suggestions
- **Phase 6 (Polish)**: Depends on Phases 3–5 complete

### User Story Dependencies

- **US1 (P1)**: Can start immediately after Phase 2. No dependencies on US2/US3.
- **US2 (P2)**: Depends on US1 (needs `ScoreAndSort` from Phase 3). `PlaylistHub.SearchLibrary` update (T019) is the only new implementation task.
- **US3 (P3)**: Depends on Phase 3 infrastructure (two-phase repo + response envelope). `GenerateSuggestions` (T022) can be implemented independently of US2.

### Within Each Phase

- All `[P]`-marked tasks within a phase share the same dependencies and touch different files — launch simultaneously.
- Tests must FAIL before their associated implementation tasks are started (TDD).
- T013 must immediately follow T012 (breaking-change migration).

---

## Parallel Execution Examples

### Phase 2 (Foundational) — launch together

```
T002 → LibraryDtos.cs (SearchSuggestionDto, LibraryResponseDto, PagedResult<T> extension)
T003 → IFuzzySearchService.cs (interface + RelevanceTier + ScoredSongResult)
T004 → LibraryState.cs (Suggestions, HasSearchedWithNoResults properties)
T005 → LibraryActions.cs (SearchSuggestionsAction)
```
Then T006 (LibraryReducers.cs) after T004 + T005 complete.

### Phase 3 (US1) — launch tests together first

```
T007 → FuzzySearchServiceTests.cs  (unit tests - ComputeOsaDistance, GetThreshold, ScoreAndSort)
T008 → LibraryApiTests.cs          (integration tests - typo queries, response object shape)
```
Then T009 → T010 + T011 (in parallel, different files) → T012 → T013 → T014 + T015 + T016 (T014 and T015 in parallel).

### Phase 4 (US2) — launch tests together

```
T017 → LibraryApiTests.cs       (ordering assertions)
T018 → LibraryPaginationTests.cs (cross-page ordering)
```
Then T019 (PlaylistHub.SearchLibrary update).

### Phase 5 (US3) — launch tests together

```
T020 → LibraryApiTests.cs     (zero-results suggestions)
T021 → LibrarySearchTests.cs  (suggestion rendering + tap)
```
Then T022 → T023 → T024 (sequential; each builds on previous).

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Baseline
2. Complete Phase 2: Foundational types
3. Complete Phase 3: US1 (fuzzy matching + response-format change)
4. **STOP and VALIDATE**: `dotnet test Karamel.Backend.Tests` green; `npm run test:run` green; manually test typo query in the running app
5. Demo / deploy. US2 and US3 can follow in subsequent iterations.

### Incremental Delivery

1. Setup + Foundational → shared types ready
2. US1 → typo tolerance live (MVP)
3. US2 → relevance ordering validated and `SearchLibrary` updated
4. US3 → "Did you mean?" suggestions live
5. Polish → serialization and observability hardened, full test suite green

### Single Developer

Work strictly in task-ID order (T001 → T028). All `[P]` markers indicate opportunistic parallelism — a solo developer can skip ahead to the next independent `[P]` task if blocked, but the natural linear sequence works cleanly.

---

## Summary

| Phase | Tasks | User Story | Parallel Opportunities |
|---|---|---|---|
| 1 — Setup | T001 | — | None |
| 2 — Foundational | T002–T006 | — | T002, T003, T004, T005 |
| 3 — US1 (P1 MVP) | T007–T016 | US1 | T007+T008 (tests); T010+T011 (impl); T014+T015 (bridge) |
| 4 — US2 (P2) | T017–T019 | US2 | T017+T018 (tests) |
| 5 — US3 (P3) | T020–T024 | US3 | T020+T021 (tests) |
| 6 — Polish | T025–T028 | — | T025, T026, T027 |
| **Total** | **28 tasks** | | |

**Suggested MVP scope**: Phases 1–3 (T001–T016) — typo-tolerant search fully delivered.
