# Tasks: Artist Exploration — Browse Mode for LibrarySearch

**Feature**: `005-artist-exploration-browse`
**Branch**: `feature/005-artist-exploration-browse`
**Plan**: [plan.md](plan.md) | **Data Model**: [data-model.md](data-model.md) | **API Contract**: [contracts/artists-api.md](contracts/artists-api.md)

**Tests**: Included — the `with-tests-workflow` is enforced (plan.md §Quality Gates).

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Parallelizable — different files, no dependency on incomplete sibling tasks
- **[US1/US2/US3]**: User story label (setup + foundational tasks carry no label)

## User Stories (derived from R2.1, R2.2, R2.4 in the annotated spec)

- **US1 (P1 — MVP)**: "As a singer, I see a full alphabetical artist list when the search box is empty"
- **US2 (P2)**: "As a singer, I can tap an artist to load their songs"
- **US3 (P3)**: "As a singer, the artist list reappears instantly after I clear my search (no re-fetch)"

---

## Phase 1: Setup

**Purpose**: Verify a clean build and test baseline before any files are modified.

- [X] T001 Verify clean build and test baseline — run `dotnet build` then `dotnet test Karamel.Web.Tests` from repo root; confirm zero build warnings and ≥ 251 tests pass

---

## Phase 2: Foundational — Backend Artist API

**Purpose**: Implement `GET /api/sessions/{id}/library/artists`. Frontend integration validation **cannot** begin until this phase is complete; bUnit (unit) tests can be written in parallel.

**⚠️ CRITICAL**: This phase blocks integration smoke-testing but does NOT block frontend unit tests.

- [X] T002 [P] Add `ArtistSummaryDto` record with `[JsonPropertyName("name")]` and `[JsonPropertyName("songCount")]` to `Karamel.Backend/Controllers/LibraryDtos.cs`
- [X] T003 [P] Add `Task<IReadOnlyList<ArtistSummaryDto>> GetArtistsAsync(Guid sessionId)` method signature to `Karamel.Backend/Repositories/ISongRepository.cs`
- [X] T004 Implement `GetArtistsAsync` in `Karamel.Backend/Repositories/EfSongRepository.cs` — LINQ `GroupBy(s => s.Artist)` + `COUNT(*)`, exclude null/whitespace artists, apply C#-side `OrderBy(OrdinalIgnoreCase)` after materialisation (depends on T003)
- [X] T005 Add `GET /api/sessions/{sessionId:guid}/library/artists` endpoint to `Karamel.Backend/Controllers/LibraryController.cs` — thin controller delegates to `ISongRepository.GetArtistsAsync`, returns `Ok(result)` (depends on T002, T004)
- [X] T006 Add `GetArtists` integration tests to `Karamel.Backend.Tests/LibraryApiTests.cs` — covers: HTTP 200 with sorted artist array for seeded session; HTTP 200 with empty array for session with no songs; correct `name`/`songCount` JSON field names in response (depends on T005)

**Checkpoint**: `curl http://localhost:5245/api/sessions/{id}/library/artists` returns a sorted JSON array. `dotnet test Karamel.Backend.Tests -v minimal` passes.

---

## Phase 3: US1 — Artist List Visible When Search Is Empty (P1 — MVP)

**Goal**: Singer opens the Library tab with no text in the search box → sees a full alphabetical artist list with song counts, fetched from the backend REST API and cached in `LibraryState`.

**Independent Test**: Open SingerView on any device with an empty search box and a loaded library → artist list renders with correct artists and counts → exactly one HTTP request is made to `/library/artists`.

### Implementation for US1

- [X] T007 [P] [US1] Create `ArtistItem` immutable record in `Karamel.Web/Models/ArtistItem.cs` — `record ArtistItem(string Name, int SongCount)`
- [X] T008 [P] [US1] Create `ArtistDto` record with `[JsonPropertyName("name")]` / `[JsonPropertyName("songCount")]` in `Karamel.Web/Contracts/ArtistDto.cs`
- [X] T009 [P] [US1] Add `LoadArtistsAction`, `LoadArtistsSuccessAction(IReadOnlyList<ArtistItem> Artists)`, and `LoadArtistsFailureAction(string ErrorMessage)` to `Karamel.Web/Store/Library/LibraryActions.cs`
- [X] T010 [US1] Add `Artists` (`IReadOnlyList<ArtistItem>`, default `Array.Empty<ArtistItem>()`), `IsLoadingArtists` (`bool`), and `ArtistsLoaded` (`bool`) fields to `Karamel.Web/Store/Library/LibraryState.cs` (depends on T007)
- [X] T011 [US1] Add artist action reducers to `Karamel.Web/Store/Library/LibraryReducers.cs`: `LoadArtistsAction` → `IsLoadingArtists = true`; `LoadArtistsSuccessAction` → set `Artists`, `IsLoadingArtists = false`, `ArtistsLoaded = true`; `LoadArtistsFailureAction` → `IsLoadingArtists = false`, `ArtistsLoaded = false`, set `ErrorMessage`; extend existing `ResetPaginationAction` reducer to also clear `Artists`, `IsLoadingArtists`, `ArtistsLoaded` (depends on T009, T010)
- [X] T011a [P] [US1] Extend existing `ScanProgressAction` reducer in `Karamel.Web/Store/Library/LibraryReducers.cs` to clear `Artists`, `IsLoadingArtists = false`, `ArtistsLoaded = false` when `ScanProgressAction.IsComplete == false` — ensures a library rescan invalidates the cached artist list so stale artists from the previous scan are not displayed (depends on T010)
- [X] T012 [US1] Add `Task<IReadOnlyList<ArtistItem>> FetchArtistsAsync(Guid sessionId)` to `ISessionApiClient` interface and implement in `SessionApiClient` — HTTP GET `{baseUrl}/api/sessions/{sessionId}/library/artists`, deserialise array as `ArtistDto[]`, map each to `new ArtistItem(dto.Name, dto.SongCount)` (depends on T007, T008)
- [X] T013 [US1] Add `HandleLoadArtistsAction` effect in `Karamel.Web/Store/Library/LibraryEffects.cs` — inject `IState<SessionState>` to read `sessionId` from `SessionState.Value.CurrentSession.SessionId`; call `FetchArtistsAsync(sessionId)`, dispatch `LoadArtistsSuccessAction` on success or `LoadArtistsFailureAction` on exception, using structured logging at Info/Error level (depends on T009, T012)
- [X] T014 [P] [US1] Add `.artist-list` and `.artist-row` CSS rules to `Karamel.Web/Components/LibrarySearch.razor.css` — artist name left-aligned, song count right-aligned in muted text, full-width tap target
- [X] T015 [US1] Add Branch C (artist browse mode) to `Karamel.Web/Components/LibrarySearch.razor` render tree — shown when `SearchFilter == ""`; displays spinner when `IsLoadingArtists`; displays artist list rows when `ArtistsLoaded` (or `Artists.Any()`); each row shows artist name + song count with `@onclick="() => SelectArtist(artist.Name)"` (depends on T010, T013, T014)

### Tests for US1

- [X] T016 [P] [US1] Write bUnit tests for artist list rendering in `Karamel.Web.Tests/ArtistBrowseTests.cs` — covers: artist rows render when `SearchFilter=""` and `ArtistsLoaded=true` with seeded `Artists`; spinner renders when `IsLoadingArtists=true`; song results table is hidden when in browse mode; `LoadArtistsAction` is dispatched on component initialise when `ScanComplete=true` and `ArtistsLoaded=false` (depends on T015)

**Checkpoint**: `dotnet test Karamel.Web.Tests` passes with new `ArtistBrowseTests` included. Manual: open SingerView → artist list visible with no search text.

---

## Phase 4: US2 — Tapping an Artist Loads Their Songs (P2)

**Goal**: Singer taps an artist row → search input populates with the artist name → song results table replaces the artist list (existing search pipeline reused).

**Independent Test**: Render artist list → click an artist row → verify `FilterSongsAction(artistName)` and `LoadPageAction(Page:1, SearchQuery:artistName, Append:false)` are both dispatched; verify search input value equals the tapped artist name; verify artist list branch is no longer rendered.

### Implementation for US2

- [ ] T017 [US2] Implement `SelectArtist(string name)` private method in `Karamel.Web/Components/LibrarySearch.razor` that dispatches `new FilterSongsAction(name)` then `new LoadPageAction(Page: 1, SearchQuery: name, Append: false)` — already wired to artist row `@onclick` from T015 (depends on T015)

### Tests for US2

- [ ] T018 [P] [US2] Write bUnit tests for artist selection in `Karamel.Web.Tests/ArtistBrowseTests.cs` — covers: clicking an artist row dispatches `FilterSongsAction` with correct name; `LoadPageAction(1, name, false)` is dispatched; artist list branch is no longer shown after dispatch; `SearchFilter` state reflects the selected artist name (depends on T017)

**Checkpoint**: `dotnet test Karamel.Web.Tests` still passes. Manual quickstart steps 1–2 verified: browse list → tap artist → songs appear.

---

## Phase 5: US3 — Artist List Returns Instantly After Clearing Search (P3)

**Goal**: After viewing songs for a selected artist, clearing the search field brings back the artist list immediately from cache — no additional network request, no loading spinner.

**Independent Test**: Browse mode → tap artist → clear search → artist list reappears with no spinner; verify `LoadArtistsAction` is **not** dispatched a second time; verify `ResetPaginationAction` (session reset) clears the artist cache and causes a fresh fetch on next browse visit.

### Implementation for US3

- [ ] T019 [US3] Add `TryLoadArtistsIfNeeded()` private helper to `Karamel.Web/Components/LibrarySearch.razor` — checks `ScanComplete && !ArtistsLoaded && !IsLoadingArtists` and dispatches `LoadArtistsAction` if true; call it from the `LibraryState.StateChanged` subscription handler, from `ClearFilter()`, and from the empty-string branch of `OnSearchInput` (depends on T015, T017)

### Tests for US3

- [ ] T020 [P] [US3] Write bUnit tests for cache and invalidation in `Karamel.Web.Tests/ArtistBrowseTests.cs` — covers: `TryLoadArtistsIfNeeded` does not dispatch when `ArtistsLoaded=true`; artist list reappears after `ClearFilter()` when `ArtistsLoaded=true` (no spinner, no action dispatched); `ResetPaginationAction` clears `Artists` and `ArtistsLoaded` in reducer (depends on T019)

**Checkpoint**: `dotnet test Karamel.Web.Tests` passes. Manual quickstart step 3 verified: clear search → list reappears instantly with no network request.

---

## Phase 6: Polish & Verification

**Purpose**: Final test suite validation, backend smoke test, and quickstart confirmation.

- [ ] T021 Run full test suites and confirm all pass: `dotnet test Karamel.Web.Tests` (≥ 251 pass, ≤ 9 skipped), `cd Karamel.Web\wwwroot; npm run test:run` (zero failures), then `cd ..\..`
- [ ] T022 [P] Run backend tests: `dotnet test Karamel.Backend.Tests -v minimal` — confirm new `GetArtists` integration tests pass
- [ ] T023 [P] Run `quickstart.md` API smoke test — `curl http://localhost:5245/api/sessions/{sessionId}/library/artists` returns correctly sorted JSON array with `name` and `songCount` fields

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS manual integration testing
- **US1 (Phase 3)**: T007–T009 and T014 are independent of Phase 2 (unit-testable in isolation); T012/T013/T015 can be written before Phase 2 completes but require Phase 2 for end-to-end validation
- **US2 (Phase 4)**: Depends on US1 (T015 must exist for T017)
- **US3 (Phase 5)**: Depends on US2 (T017 state needed for T019 call sites)
- **Polish (Phase 6)**: Depends on all user stories complete

### Within US1 (Phase 3)

```
T007, T008, T009, T014  ──── parallel (independent files)
T010 ─── depends on T007
T011 ─── depends on T009, T010
T011a ── depends on T010 (parallel with T011 — different reducer branch)
T012 ─── depends on T007, T008
T013 ─── depends on T009, T012
T015 ─── depends on T010, T011, T011a, T013, T014
T016 ─── depends on T015
```

### Parallel Opportunities

```
# Phase 2 — start these in parallel:
T002: Add ArtistSummaryDto to LibraryDtos.cs
T003: Add GetArtistsAsync signature to ISongRepository.cs

# Phase 3 — fast-start parallel entry tasks:
T007: ArtistItem.cs
T008: ArtistDto.cs
T009: LibraryActions.cs additions
T014: LibrarySearch.razor.css additions
```

---

## Implementation Strategy

### MVP Scope (US1 Only)

1. Complete Phase 1: Baseline check
2. Complete Phase 2: Backend endpoint (immediately curl-testable)
3. Complete Phase 3: US1 state + component + tests
4. **STOP and VALIDATE**: Artist list visible in browser on any device → deploy or demo

### Full Feature (Sequential)

1. MVP (US1 above)
2. Phase 4 — US2: Tap interaction + tests
3. Phase 5 — US3: Cache hit + invalidation + tests
4. Phase 6: Final validation suite + quickstart smoke test

### Parallel Team Strategy

Once Phase 2 is complete:
- Developer A: US1 (Phases 3) — state layer + rendering
- Developer B: US2 (Phase 4) + US3 (Phase 5) — interactions + cache (wait for US1 T015)
