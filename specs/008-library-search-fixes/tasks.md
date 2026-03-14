# Tasks: Library Search UX Fixes

**Input**: Design documents from `/specs/008-library-search-fixes/`
**Prerequisites**: plan.md ✅, spec.md ✅, quickstart.md ✅

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths included in all descriptions

---

## Phase 1: Setup

**N/A** — All three fixes target existing source files. No new project structure, dependencies, or configuration is required.

---

## Phase 2: Foundational (Blocking Prerequisites)

**N/A** — All three user stories are fully independent; none block any other. All can start after a clean build is confirmed.

---

## Phase 3: User Story 1 — Artist-Name Search Fix (Priority: P1) 🎯 MVP

**Goal**: Fix the fuzzy candidate pool in `EfSongRepository` so that artists sorted late alphabetically (Q–Z) are included in results when the user searches by artist name.

**Independent Test**: `dotnet test Karamel.Backend.Tests -v minimal` passes the new integration test `GetPage_FuzzySearch_IncludesArtistMatchesBeyondAlphabeticalPosition500`, and "Bohemian Rhapsody" / "Somebody to Love" by "Queen" appear in results for the search term "Queen" in a 600-song library.

### Tests for User Story 1

> Write this test FIRST and confirm it FAILS before touching production code.

- [X] T001 [US1] Add failing integration test `GetPage_FuzzySearch_IncludesArtistMatchesBeyondAlphabeticalPosition500` to `Karamel.Backend.Tests/LibraryApiTests.cs` — seed 600 songs spanning artists A–Z with "Queen" owning songs "Bohemian Rhapsody" and "Somebody to Love" at alphabetical position ~550, plus "Dancing Queen" by "ABBA" at position ~1; assert the GET `/api/sessions/{id}/library?search=Queen` response contains all three songs and "Dancing Queen" ranks above "Bohemian Rhapsody" (PartialTitle before ArtistOnly tier)

### Implementation for User Story 1

- [X] T002 [US1] In `Karamel.Backend/Repositories/EfSongRepository.cs`, locate the fuzzy execution path (`if (useFuzzy)` branch) and replace the unfiltered `allCandidates` query with a `EF.Functions.Like`-filtered query: add `.Where(s => EF.Functions.Like(s.Artist, $"%{trimmedSearch}%") || EF.Functions.Like(s.Title, $"%{trimmedSearch}%"))` before `.OrderBy(s => s.Artist).ThenBy(s => s.Title).Take(IFuzzySearchService.MaxCandidateForFuzzy)`

**Checkpoint**: Run `dotnet test Karamel.Backend.Tests -v minimal` — T001 must now pass; all other backend tests must remain green.

---

## Phase 4: User Story 2 — Sticky Search Box (Priority: P2)

**Goal**: Keep the search input anchored to the top of the viewport so the user can refine their search without scrolling back to the top when results overflow the screen.

**Independent Test**: Open Singer page, search for a common word (e.g. "love") to get many results, scroll to the bottom — the search input is still visible and interactive at the top of the viewport.

### Implementation for User Story 2

- [X] T003 [P] [US2] In `Karamel.Web/Components/LibrarySearch.razor.css`, add sticky positioning to the `.library-search .search-box` rule: `position: sticky; top: 0; z-index: 10; background-color: var(--color-bg, #fff); padding-bottom: 0.5rem; margin-bottom: 0.5rem;`

**Checkpoint**: Manually verify the search box stays visible at the top of the screen after scrolling past 50+ results in SingerView.

---

## Phase 5: User Story 3 — Fixed Background Gradient (Priority: P3)

**Goal**: Replace the `html { background-attachment: fixed }` approach with a `body::before` fixed pseudo-element so the gradient covers exactly the viewport, looks identical with 2 results or 3000 results, and does not scroll with the page content.

**Independent Test**: Manually compare the gradient appearance with 3 search results vs. 500+ results — visually identical in both cases; gradient does not move when scrolling.

### Implementation for User Story 3

- [X] T004 [P] [US3] In `Karamel.Web/wwwroot/css/tokens.css` (light-mode block): remove `background`, `background-attachment`, and `background-size` from the `html` rule; add `body::before { content: ''; position: fixed; inset: 0; background: var(--gradient-light); z-index: -1; pointer-events: none; }`
- [X] T005 [P] [US3] In `Karamel.Web/wwwroot/css/tokens.css` (dark-mode blocks): inside the existing `@media (prefers-color-scheme: dark)` and `[data-theme="dark"]` selectors, add `body::before { background: var(--gradient-dark); }` to override the pseudo-element for dark mode

**Checkpoint**: Visually confirm dark and light themes both show an identical, viewport-pinned gradient regardless of result count, and that scrolling long result lists does not cause the gradient to move.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate all three fixes together and confirm no regressions.

- [X] T006 Run `dotnet build` from solution root and verify zero warnings
- [X] T007 [P] Run `dotnet test Karamel.Backend.Tests -v minimal` and confirm all tests pass, including `GetPage_FuzzySearch_IncludesArtistMatchesBeyondAlphabeticalPosition500`
- [X] T008 [P] Run `dotnet test Karamel.Web.Tests` and confirm ≥ 260 tests pass with ≤ 9 skipped (no regressions from CSS changes)
- [X] T009 [P] Run `cd Karamel.Web\wwwroot; npm run test:run; cd ..\.` and confirm zero JavaScript test failures (no JS changes expected)
- [ ] T010 Run the manual `quickstart.md` verification steps for UC1 (artist-name search), UC2 (sticky search box), and UC3 (gradient consistency)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 & 2**: Skipped (no setup or foundational work needed)
- **Phase 3 (US1)**: Start immediately — backend-only change
- **Phase 4 (US2)**: Start immediately — independent CSS change in a different file from US3
- **Phase 5 (US3)**: Start immediately — independent CSS change in a different file from US2
- **Phase 6 (Polish)**: Depends on all chosen user stories being complete

### User Story Dependencies

- **US1 (P1)**: Independent — no dependencies on US2 or US3
- **US2 (P2)**: Independent — no dependencies on US1 or US3
- **US3 (P3)**: Independent — no dependencies on US1 or US2

### Within User Story 1

- T001 (test) MUST be written and confirmed FAILING before T002 (implementation)
- T002 implementation completes when T001 passes

### Parallel Opportunities

- US2 and US3 tasks (T003, T004, T005) are all in different files — can be implemented simultaneously
- US1 can proceed in parallel with US2 and US3 (different codebase layers: backend vs. frontend CSS)

---

## Parallel Example: All Three Stories

```powershell
# All three can be worked concurrently (different files, different layers):
# Developer / session A:  T001 → T002   (backend EfSongRepository + test)
# Developer / session B:  T003          (LibrarySearch.razor.css)
# Developer / session C:  T004 → T005   (tokens.css light + dark)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 3 (US1): write test → fix repository query → confirm test passes
2. **STOP and VALIDATE**: `dotnet test Karamel.Backend.Tests -v minimal` passes
3. Deploy if ready — artist-name search is now functionally correct

### Incremental Delivery

1. US1 (P1) → Test independently → Deploy (functional fix)
2. US2 (P2) → Smoke-test manually → Deploy (UX fix)
3. US3 (P3) → Smoke-test manually → Deploy (visual fix)
4. Each story adds value without affecting the others

### Summary

| Task | Story | File | Type |
|------|-------|------|------|
| T001 | US1 | `Karamel.Backend.Tests/LibraryApiTests.cs` | Test (write first) |
| T002 | US1 | `Karamel.Backend/Repositories/EfSongRepository.cs` | Backend fix |
| T003 | US2 | `Karamel.Web/Components/LibrarySearch.razor.css` | CSS fix |
| T004 | US3 | `Karamel.Web/wwwroot/css/tokens.css` | CSS fix (light) |
| T005 | US3 | `Karamel.Web/wwwroot/css/tokens.css` | CSS fix (dark) |
| T006–T010 | — | Various | Validation |
