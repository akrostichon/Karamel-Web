# Tasks: Library View Enhancements

**Input**: Design documents from `/specs/006-library-view-enhancements/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, quickstart.md ✓

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete-task dependencies)
- **[Story]**: Which user story this task belongs to ([US1], [US2])
- File paths are relative to the repository root

---

## Phase 1: Setup (Baseline Verification)

**Purpose**: Confirm a clean starting state and the correct feature branch before any changes.

- [X] T001 Verify build and test baseline: run `dotnet build` (zero warnings), `dotnet test Karamel.Web.Tests` (≥260 passing, ≤9 skipped), and `npm run test:run` from `Karamel.Web/wwwroot` (zero failures); confirm branch is `006-library-view-enhancements`

---

## Phase 2: Foundational (N/A — Skipped)

No blocking foundational infrastructure is required. Both user stories are CSS / Blazor rendering changes within existing files, with no shared models, no backend changes, and no new Fluxor state.

---

## Phase 3: User Story 1 — Jump to Artist by Letter (Priority: P1) 🎯 MVP

**Goal**: Artist browse mode gains a fixed vertical A–Z strip on the right edge. Tapping an active letter instantly scrolls to the corresponding section header in the artist list. Letters with no matching artists are dimmed and non-interactive. Per-letter section headers group the artist rows for visual context after navigation.

**Independent Test**: Open artist browse with a library containing artists spread across several letters. Verify: the 26-letter strip is visible; tapping an active letter scrolls to that letter's section header; inactive letters cannot be tapped; per-letter section headers appear above each artist group; the strip is hidden when artist browse mode is not active.

### Implementation for User Story 1

- [X] T002 [P] [US1] Create `Karamel.Web/wwwroot/js/alphabetBridge.js` — export `scrollToArtistSection(letter)` that calls `element.scrollIntoView({ behavior: 'instant', block: 'start' })` on `#letter-{letter}`; use `createLogger('AlphabetBridge')` and log a warning if the target element is not found
- [X] T003 [P] [US1] Add `ArtistGroup` private record, `_artistGroups` (`IReadOnlyList<ArtistGroup>`), `_activeLetters` (`HashSet<char>`), `_alphabetModule` (`IJSObjectReference?`), `BuildArtistGroups()`, `OnAfterRenderAsync` (imports `alphabetBridge.js` on first render), and `ScrollToLetter(char)` async method to `Karamel.Web/Components/LibrarySearch.razor` `@code` section; call `BuildArtistGroups()` when `ArtistsLoaded` transitions to `true`
- [X] T004 [P] [US1] Add `.artist-browse` (padding-right for strip clearance), `.artist-section-header` (`position: sticky; top: 0`), `.alphabet-bar` (`position: fixed` right-side vertical strip), and `.alpha-btn` active/inactive/disabled styles to `Karamel.Web/Components/LibrarySearch.razor.css`
- [X] T005 [US1] Replace the flat artist list template in `Karamel.Web/Components/LibrarySearch.razor` with grouped rendering: wrap in `<div class="artist-browse">`; `@foreach` over `_artistGroups` → section header `<div id="letter-@group.Letter" class="artist-section-header">` + artist rows; add fixed vertical `<nav class="alphabet-bar">` with 26 `<button>` elements (active/disabled determined by `_activeLetters`); add `_alphabetModule?.DisposeAsync()` call in `DisposeAsyncCore` (depends on T003, T004)

### Tests for User Story 1

- [X] T006 [P] [US1] Create `Karamel.Web/wwwroot/js/alphabetBridge.test.js` with Vitest tests: (1) `scrollToArtistSection('S')` when `#letter-S` is in the DOM → `scrollIntoView` is called with `{ behavior: 'instant', block: 'start' }`; (2) `scrollToArtistSection('Z')` when `#letter-Z` is absent → `logger.warn` is invoked and no exception is thrown (depends on T002)
- [X] T007 [US1] Extend `Karamel.Web.Tests/ArtistBrowseTests.cs` with bUnit tests: 26 letter buttons rendered when artists are loaded; active letters are enabled, inactive letters have `disabled` attribute; one section header rendered per unique first-letter group; tapping an active letter invokes the `scrollToArtistSection` JSInterop call with the correct string argument; alphabet bar is absent when the library is not scanned or browse mode is not active. Artists in the `#` group (non-alpha first character) must appear in the rendered list but are not required to have a corresponding alphabet bar button (depends on T005, T006)

### Scroll-Following Letter Highlight (Acceptance Scenario 4)

- [X] T014 [P] [US1] Add `observeArtistSections(dotNetRef)` and `disconnectArtistSectionObserver()` exports to `Karamel.Web/wwwroot/js/alphabetBridge.js` — create an `IntersectionObserver` watching all `.artist-section-header` elements (threshold `0`, `rootMargin: '-1px 0px -90% 0px'`) that calls `dotNetRef.invokeMethodAsync('OnLetterVisible', letter)` when a section enters view; `disconnectArtistSectionObserver` calls `observer.disconnect()` and clears the reference (depends on T002)
- [X] T015 [US1] Add `_currentLetter` (`char?`) field, `[JSInvokable] OnLetterVisible(string letter)` method (sets `_currentLetter`, calls `StateHasChanged`), and Dispose-safe `DotNetObjectReference` management (`_dotNetRef`) to `Karamel.Web/Components/LibrarySearch.razor` `@code`; call `observeArtistSections` after the grouped markup is rendered (in `OnAfterRenderAsync` when `_artistGroups` count changes); call `disconnectArtistSectionObserver` and dispose `_dotNetRef` in `DisposeAsyncCore`; bind `_currentLetter` to an `.alpha-btn--current` CSS class in the alphabet bar markup (depends on T005, T014)
- [X] T016 [P] [US1] Add `.alpha-btn--current` style (distinct highlight colour, e.g. `background: rgba(var(--color-primary-rgb), 0.15); color: var(--color-primary)`) to `Karamel.Web/Components/LibrarySearch.razor.css`; extend `alphabetBridge.test.js` with Vitest tests for `observeArtistSections` (mock `IntersectionObserver`, verify `invokeMethodAsync` called with correct letter on intersection) and `disconnectArtistSectionObserver` (verifies `observer.disconnect` called) (depends on T014)

- [X] T017 [US1] Extend `Karamel.Web.Tests/ArtistBrowseTests.cs`: verify that when `OnLetterVisible("S")` is invoked, the `S` button receives the `.alpha-btn--current` class and other letter buttons do not (depends on T015, T016)

**Checkpoint**: Open SingerView in artist browse mode with a multi-letter library. The A–Z strip is visible on the right. Tapping an active letter scrolls the list to the correct section header.

---

## Phase 4: User Story 2 — Stable Gradient When Scrolling or Loading More (Priority: P2)

**Goal**: The background gradient is anchored to the viewport so it never shifts or jumps when content height grows (Load More) or when the user scrolls up/down.

**Independent Test**: Open library search with results loaded. Tap "Load More" several times. Scroll up and down. The gradient is visually identical throughout — no jump, no repositioning.

### Implementation for User Story 2

- [X] T008 [P] [US2] Add `background-attachment: fixed;` and `background-size: cover;` to the `html` rule in `Karamel.Web/wwwroot/css/tokens.css` — root cause: `html` grows with content, stretching the `linear-gradient` diagonal (see research.md Finding 1)
- [X] T009 [P] [US2] Add `background-attachment: fixed;` to the `.singer-header` rule in `Karamel.Web/Pages/SingerView.razor.css` so the header gradient is viewport-anchored to match the `html` background and produce a seamless blend
- [X] T010 [US2] Visual verification — run `dotnet run --project Karamel.Web`, navigate to SingerView with ≥50 songs loaded, tap "Load More" multiple times and scroll up/down; confirm the gradient appearance is visually unchanged throughout (depends on T008, T009)

**Checkpoint**: Load More appends rows. The gradient is frozen. No jump visible at any scroll position.

---

## Phase 5: Polish & Validation

**Purpose**: Full test suite pass after both user stories are complete.

- [X] T011 [P] Run `dotnet build` from solution root — confirm zero errors, zero warnings
- [X] T012 Run full C# test suite: `dotnet test Karamel.Web.Tests` — confirm ≥260 passing, ≤9 skipped
- [X] T013 [P] Run full JS test suite: `npm run test:run` from `Karamel.Web/wwwroot` — confirm zero failures

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **User Story 1 (Phase 3)**: Depends on Phase 1 completion only — no foundational blocking tasks
- **User Story 2 (Phase 4)**: Depends on Phase 1 completion only — fully independent of US1
- **Polish (Phase 5)**: Depends on Phase 3 and Phase 4 both complete

### User Story Dependencies

- **US1 (P1)** and **US2 (P2)** are fully independent — they touch disjoint files and can be implemented in any order or in parallel
- **Quickstart note**: Consider doing US2 (T008–T010) before US1 — it is a pure CSS change that validates the clean baseline before tackling the JS interop work

### Within User Story 1

```
T001 (baseline)
  ├── T002 (alphabetBridge.js) ─────────────────────────────→ T006 (JS tests)
  │     └──→ T014 (observeArtistSections) ──→ T016 (CSS + observer tests)
  ├── T003 (LibrarySearch.razor @code) ──┐
  └── T004 (LibrarySearch.razor.css)  ───┴──→ T005 (markup + dispose)
                                                  ├──→ T007 (bUnit tests)
                                                  └──→ T015 (scroll-tracking @code) ──→ T017 (bUnit scroll tests)
```

### Within User Story 2

```
T001 (baseline)
  ├── T008 (tokens.css) ──────┐
  └── T009 (SingerView.css) ──┴──→ T010 (visual verification)
```

### Full Graph (earliest start)

```
T001
  ├──→ T002 ──→ T006 ──→ T007 (wait for T005)
  │      └──→ T014 ──→ T016
  ├──→ T003 ──┐
  ├──→ T004 ──┴──→ T005 ──→ T007
  │                   └──→ T015 (depends T014) ──→ T017
  ├──→ T008 ──┐
  └──→ T009 ──┴──→ T010
T006, T007, T010, T016, T017 ──→ T011, T012, T013
```

---

## Parallel Execution Examples

### User Story 1 — tasks that can start simultaneously after T001

```bash
# Parallel batch 1 (disjoint files):
T002: Create Karamel.Web/wwwroot/js/alphabetBridge.js
T003: Add @code additions to Karamel.Web/Components/LibrarySearch.razor
T004: Add CSS to Karamel.Web/Components/LibrarySearch.razor.css

# After T003 + T004 complete:
T005: Update LibrarySearch.razor markup

# After T002 completes (can overlap with T003/T004/T005 work):
T006: Create Karamel.Web/wwwroot/js/alphabetBridge.test.js
T014: Add Intersection Observer exports to alphabetBridge.js

# After T005 + T006 complete:
T007: Extend Karamel.Web.Tests/ArtistBrowseTests.cs (core alphabet bar tests)

# After T014 completes (can overlap with T005/T007 work):
T016: Add .alpha-btn--current CSS + observer Vitest tests

# After T005 + T014 complete:
T015: Add scroll-tracking @code + DotNetObjectReference to LibrarySearch.razor

# After T015 + T016 complete:
T017: Extend ArtistBrowseTests.cs (scroll-highlight test)
```

### User Story 2 — tasks that can start simultaneously after T001

```bash
# Parallel batch (disjoint files):
T008: Karamel.Web/wwwroot/css/tokens.css
T009: Karamel.Web/Pages/SingerView.razor.css

# After T008 + T009 complete:
T010: Visual verification in running app
```

### Both user stories in parallel (two developers)

```
Developer A: T002 → T003, T004 → T005 → T006 → T007
Developer B: T008, T009 → T010
Both: T011, T012, T013
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (baseline)
2. Complete Phase 3 (US1 — alphabet navigation + scroll-following highlight)
3. **STOP and VALIDATE**: run targeted C# and JS tests, test in browser
4. Optionally demo/review before adding US2

### Recommended Solo Order (lowest risk first)

1. Phase 1 — verify baseline
2. **Phase 4 (US2 first)** — two CSS lines, zero JS risk, confirms clean baseline
3. Phase 3 (US1) — JS interop + Blazor rendering
4. Phase 5 — full suite validation

### Incremental Delivery

| After completing… | Deliverable |
|---|---|
| T001 | Confirmed clean baseline |
| T008 + T009 + T010 | Gradient is stable (US2 shipped) |
| T002–T007 | A-Z letter navigation live, static active state (US1 core shipped) |
| T014–T017 | Scroll-following letter highlight live (US1 fully complete) |
| T011–T013 | Full feature validated, ready to commit |
