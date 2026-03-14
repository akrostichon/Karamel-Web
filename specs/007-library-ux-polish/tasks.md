# Tasks: Library UX Polish

**Input**: Design documents from `/specs/007-library-ux-polish/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

**Tests**: Included — spec and plan both require tests in `ArtistBrowseTests.cs` and `alphabetBridge.test.js`.

**Organization**: Tasks are grouped by user story. Each story is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unmet dependencies)
- **[Story]**: Traces task to a user story (US1–US5)
- All paths are relative to the solution root `Karamel-Web/`

---

## Phase 1: Setup (Verify Baseline)

**Purpose**: Confirm the starting state is clean before any changes are made.

- [X] T001 Verify `dotnet build` passes with zero warnings and `dotnet test Karamel.Web.Tests` reports ≥ 260 passing / 9 skipped

**Checkpoint**: Baseline confirmed — ready to begin foundational work

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add the shared Fluxor state fields and action types that User Stories 1 and 3 both depend on. Must be complete before Phase 3 or Phase 5 can begin.

**⚠️ CRITICAL**: US1 and US3 implementations cannot start until this phase is complete.

- [X] T002 [P] Add `IsLoadingArtistSongs` (`bool`, default `false`) and `ArtistSongsError` (`string?`, default `null`) properties to `Karamel.Web/Store/Library/LibraryState.cs`
- [X] T003 [P] Add `SelectArtistAction(string ArtistName)` and `LoadPageFailureAction(string ErrorMessage)` records to `Karamel.Web/Store/Library/LibraryActions.cs`
- [X] T004 Add `ReduceSelectArtistAction` (sets `SearchFilter`, `IsLoadingArtistSongs = true`, `ArtistSongsError = null`) and `ReduceLoadPageFailureAction` (sets `IsLoadingArtistSongs = false`, `ArtistSongsError`); extend `ReduceLoadPageSuccess` to reset both new fields; extend `ReduceFilterSongsAction` to clear both fields when filter is cleared in `Karamel.Web/Store/Library/LibraryReducers.cs`
- [X] T005 Extend `HandleLoadPageAction` in `Karamel.Web/Store/Library/LibraryEffects.cs` to catch exceptions and dispatch `new LoadPageFailureAction("Could not load songs. Tap to retry.")` in the catch block

**Checkpoint**: Fluxor state foundation complete — US1 and US3 can now begin

---

## Phase 3: User Story 1 — Loading Spinner on Artist Drill-In (Priority: P1) 🎯 MVP

**Goal**: Replace the empty-state flash with a spinner when an artist is tapped, and show a retry card on fetch failure.

**Independent Test**: Throttle the network in Chrome DevTools to "Slow 4G". Tap any artist. Verify a spinner appears immediately; the song list appears on success; an inline retry message appears on failure. "No songs in library" and "No songs match" are never shown while loading.

### Implementation for User Story 1

- [X] T006 [US1] Update `SelectArtist(string name)` in `Karamel.Web/Components/LibrarySearch.razor` to dispatch `SelectArtistAction(name)` instead of `FilterSongsAction(name)`; add `string? _lastSelectedArtist` component field; assign `_lastSelectedArtist = name` before dispatching
- [X] T007 [US1] Add `IsLoadingArtistSongs` spinner branch and `ArtistSongsError` error-card branch (with retry button that re-dispatches `SelectArtistAction(_lastSelectedArtist!)`) to the rendering chain in `Karamel.Web/Components/LibrarySearch.razor`, positioned after the global `ErrorMessage` branch and before the empty-state check

### Tests for User Story 1

- [X] T008 [US1] Add bUnit tests for spinner visibility on artist tap, spinner dismissal on `LoadPageSuccessAction`, and absence of empty-state messages while `IsLoadingArtistSongs = true` to `Karamel.Web.Tests/ArtistBrowseTests.cs`
- [X] T009 [US1] Add bUnit tests for error card appearance on `LoadPageFailureAction`, retry button re-dispatching `SelectArtistAction`, and spinner dismissal on failure to `Karamel.Web.Tests/ArtistBrowseTests.cs`

**Checkpoint**: US1 fully functional and tested — spinner appears on every artist drill-in, error handling works

---

## Phase 4: User Story 2 — Scroll Position Restored on Filter Clear (Priority: P2)

**Goal**: Remember the artist-list scroll offset when an artist is tapped, then restore it when the X button clears the filter.

**Independent Test**: Open artist browse with 30+ artists. Scroll to the "L–N" range. Tap an artist. Tap the X button. Verify the artist list is restored to the same scroll position.

### Implementation for User Story 2

- [X] T010 [P] [US2] Add `export function getScrollY() { return window.scrollY; }` and `export function scrollToY(y) { window.scrollTo({ top: y, behavior: 'instant' }); }` to `Karamel.Web/wwwroot/js/alphabetBridge.js`
- [X] T011 [US2] Add `double _savedScrollY` and `bool _needsScrollRestore` component fields to `Karamel.Web/Components/LibrarySearch.razor`; in `SelectArtist()`, call `getScrollY` via JS interop and store the result in `_savedScrollY` before dispatching; in `ClearFilter()`, set `_needsScrollRestore = true`; in `OnAfterRenderAsync`, when `_needsScrollRestore` is `true`, call `scrollToY(_savedScrollY)` then clear both fields

### Tests for User Story 2

- [X] T012 [P] [US2] Add Vitest tests for `getScrollY` (returns `window.scrollY`) and `scrollToY` (calls `window.scrollTo` with `{ top: y, behavior: 'instant' }`) to `Karamel.Web/wwwroot/js/alphabetBridge.test.js`
- [X] T013 [P] [US2] Add bUnit tests for `_savedScrollY` captured on artist tap, `scrollToY` invoked after filter clear, and no restore on fresh component mount to `Karamel.Web.Tests/ArtistBrowseTests.cs`

**Checkpoint**: US2 fully functional and tested — scroll position is remembered and restored

---

## Phase 5: User Story 3 — Accurate Empty State Messages (Priority: P3)

**Goal**: "No songs in library" is shown only when `TotalCount == 0` with no active filter; "No songs match" is shown when a filter or search yields zero results; no empty-state is shown while loading.

**Independent Test**: In a library with songs, type a search term matching nothing — verify only "No songs match your search criteria." appears. Test with an empty library — verify "No songs in library." appears.

### Implementation for User Story 3

- [X] T014 [US3] Replace the `Songs.Any()` empty-state discriminator in `Karamel.Web/Components/LibrarySearch.razor` with logic based on `TotalCount` and `SearchFilter`: show nothing when `IsLoading = true` (text search in flight); show "No songs match your search criteria." when `SearchFilter` is non-empty and `FilteredAndSortedSongs` is empty; show "No songs in library." only when `TotalCount == 0` and `SearchFilter` is empty; guard the entire empty-state block with `!IsLoadingArtistSongs && ArtistSongsError == null`. **Requires T007 to be complete first** — the guard is inserted below the spinner/error-card branches T007 adds.

### Tests for User Story 3

- [X] T015 [P] [US3] Add bUnit tests for all 5 scenarios (empty library, search-no-match, filter-no-match, text-search-in-flight shows nothing, clearing search shows songs) using `TotalCount` and `SearchFilter` states to `Karamel.Web.Tests/ArtistBrowseTests.cs`

**Checkpoint**: US3 fully functional and tested — empty-state messages are always accurate

---

## Phase 6: User Story 4 — A-Z Marker Stays in Sync After Letter Jump (Priority: P4)

**Goal**: Tapping a letter button in the alphabet bar immediately highlights that letter without waiting for the `IntersectionObserver` callback.

**Independent Test**: Open artist browse with artists across many letters. Tap "S". Verify "S" is highlighted immediately. Tap "A". Verify "A" highlights and "S" un-highlights immediately.

### Implementation for User Story 4

- [X] T016 [US4] In `ScrollToLetter(char letter)` in `Karamel.Web/Components/LibrarySearch.razor`, add `_currentLetter = letter; StateHasChanged();` immediately before the `scrollToArtistSection` JS interop call so the highlight updates in the same render cycle as the scroll

### Tests for User Story 4

- [X] T017 [P] [US4] Add bUnit tests verifying `_currentLetter` is set to the tapped letter immediately upon `ScrollToLetter` invocation (scenarios: single tap, repeated same-letter tap, jump from R to A) to `Karamel.Web.Tests/ArtistBrowseTests.cs`

**Checkpoint**: US4 fully functional and tested — alphabet bar highlight syncs immediately on every letter tap

---

## Phase 7: User Story 5 — A-Z Bar Fills Full Vertical Height (Priority: P4)

**Goal**: The alphabet bar stretches from top to bottom of the viewport with letters evenly distributed, eliminating the blank gap below the last letter.

**Independent Test**: Open artist browse in a tall portrait viewport. Verify the A-Z bar fills the full viewport height with evenly spaced letters. Rotate to landscape — verify letters re-distribute to the new height.

### Implementation for User Story 5

- [X] T018 [US5] Update `.alphabet-bar` in `Karamel.Web/Components/LibrarySearch.razor.css` to `height: calc(100vh - 2rem)`, `align-self: auto`, `justify-content: space-evenly`, and remove `gap: 1px`; update `.alpha-btn` to add `display: flex; justify-content: center; align-items: center;` and remove fixed `height: 1.6rem` and `line-height: 1.6rem`

### Tests for User Story 5

- [X] T019 [P] [US5] Add bUnit test verifying the `.alphabet-bar` element is rendered in the artist browse view to `Karamel.Web.Tests/ArtistBrowseTests.cs` (CSS layout validation is manual via quickstart.md)

**Checkpoint**: US5 fully functional — alphabet bar fills full height on all screen sizes

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all five stories before the feature is considered done.

- [X] T020 Run `dotnet test Karamel.Web.Tests` and confirm ≥ 260 tests pass with 9 skipped
- [X] T021 [P] Run `cd Karamel.Web\wwwroot; npm run test:run` and confirm 0 failures; then return to solution root
- [X] T022 Run through the quickstart.md manual test checklist against `dotnet run --project Karamel.Web` for all five fixes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — verify baseline immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS Phase 3 and Phase 5**
- **Phase 3 (US1)**: Depends on Phase 2 (needs `IsLoadingArtistSongs`, `SelectArtistAction`, reducers, effects)
- **Phase 4 (US2)**: T010 is independent of all story phases; **T011 depends on T006 (Phase 3)** because both modify `SelectArtist()` in the same file
- **Phase 5 (US3)**: Depends on Phase 2 (needs `IsLoadingArtistSongs` guard) **and T007 (Phase 3)** — T014 inserts the empty-state block below the spinner/error branches added by T007
- **Phase 6 (US4)**: Depends on Phase 1 only — one-line change to `ScrollToLetter()`
- **Phase 7 (US5)**: Depends on Phase 1 only — pure CSS changes
- **Phase 8 (Polish)**: Depends on all story phases being complete

### User Story Dependencies

| Story | Depends On | Blocked By |
|-------|-----------|------------|
| US1 (P1) | Phase 2 complete | T004, T005 |
| US2 (P2) | T006 (Phase 3) for T011; Phase 1 for T010 | T006 (Phase 3) |
| US3 (P3) | Phase 2 complete + T007 (Phase 3) | T004, T007 |
| US4 (P4) | Phase 1 only | None |
| US5 (P4) | Phase 1 only | None |

### Within Each User Story

- Implementation tasks before test tasks (with-tests workflow)
- T011 must follow T006 (both modify `SelectArtist()`)
- T014 must follow T007 (T014 inserts below branches T007 adds) and Phase 2 (reads `IsLoadingArtistSongs` state)

### Parallel Opportunities

- T002 and T003 (Phase 2) can run in parallel — different files
- T004 and T005 can run in parallel after T002+T003 — different files
- T008 and T009 (US1 tests) can run in parallel — both add to the same test file but test different scenarios
- T010, T012 (US2 JS) can run in parallel — `alphabetBridge.js` and `alphabetBridge.test.js`
- T016, T018, T019 are independent of all other stories

---

## Parallel Example: Foundational Phase

```text
# T002 and T003 can start immediately in parallel (different files):
Task T002: Add IsLoadingArtistSongs, ArtistSongsError to LibraryState.cs
Task T003: Add SelectArtistAction, LoadPageFailureAction to LibraryActions.cs

# After both T002 and T003 complete, T004 and T005 can run in parallel:
Task T004: Add/update reducers in LibraryReducers.cs
Task T005: Extend effects in LibraryEffects.cs
```

## Parallel Example: While US1 Is In Progress

```text
# US4 and US5 can run concurrently with US1 (no shared dependencies):
Task T016: ScrollToLetter direct assignment in LibrarySearch.razor
Task T018: CSS fixes in LibrarySearch.razor.css
Task T019: Alphabet bar render test in ArtistBrowseTests.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Verify baseline
2. Complete Phase 2: Foundational state (CRITICAL — blocks US1)
3. Complete Phase 3: User Story 1 (spinner + error card)
4. **STOP and VALIDATE**: Run `dotnet test --filter ArtistBrowseTests`; manually test spinner on a throttled connection
5. Ship if ready — singers no longer see confusing empty-state flashes

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready
2. Phase 3 (US1) → Spinner works → Deploy/demo (MVP!)
3. Phase 4 (US2) → Scroll restore → Deploy/demo
4. Phase 5 (US3) → Accurate messages → Deploy/demo
5. Phase 6 (US4) → A-Z sync → Deploy/demo
6. Phase 7 (US5) → A-Z full height → Deploy/demo
7. Phase 8 → Final validation

Each phase adds value without breaking previous fixes.

---

## Notes

- [P] tasks = different files, no unmet dependencies — safe to run in parallel
- [Story] label traces back to spec.md user stories for acceptance criteria
- No backend changes — all 22 tasks touch Blazor WASM, Fluxor store, JS, or CSS only
- Tests follow with-tests workflow: implementation first, tests immediately after within the same story phase
- `ArtistBrowseTests.cs` accumulates tests for all five stories — existing tests (if any) must continue passing
- Scroll restore (US2) test is a JSInterop mock test; may be skipped if bUnit async JSInterop limitations apply (consistent with existing 9 skipped tests policy)
