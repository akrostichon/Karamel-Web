# Tasks: Library CSV Export

**Input**: Design documents from `/specs/011-library-csv-export/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/export-contracts.md ✅, quickstart.md ✅

**Tests**: Included — spec requires tests written alongside production code; explicit test files defined in plan.md.

**Organization**: Tasks are grouped by user story. US4 (page shell / session-independence) is implemented first as the structural foundation that US1, US2, and US3 build upon.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4 from spec.md)
- Exact file paths included in all descriptions

---

## Phase 1: Setup

**Purpose**: Verify a clean baseline on the feature branch before any new files are added.

- [X] T001 Verify `dotnet build` is warning-free and `dotnet test Karamel.Web.Tests` passes (≥ 260 tests, ≤ 9 skipped) on branch `feature/011-library-csv-export`

**Checkpoint**: Green baseline confirmed — implementation can begin.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core C# helper and JavaScript bridge. ALL user story phases depend on these being complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 Implement `EscapeCsvField` (RFC 4180, semicolons as delimiter), `NormalizeForComparison` (lowercase, strip articles `the`/`a`/`an`, strip punctuation `/`, `-`, `'`, `"`, `,`, `.`, collapse whitespace), and `OsaDistance(string a, string b, int earlyExitThreshold)` (OSA algorithm with early-exit returning `threshold + 1` when exceeded) in `Karamel.Web/Helpers/CsvExportHelper.cs`
- [X] T003 [P] Create `exportBridge.js` ES module with `scanDirectory(filenamePattern)` (static-imports `pickLibraryDirectory` from `./fileAccess.js`, logs via `createLogger('ExportBridge')`) and `triggerDownload(content, filename)` (`Blob` → object URL → `<a>` click → `URL.revokeObjectURL`) in `Karamel.Web/wwwroot/js/exportBridge.js`
- [X] T004 Write Vitest unit tests for `scanDirectory` (mock `fileAccess.js`, verifies songs returned and error propagated on cancel) and `triggerDownload` (mock `URL.createObjectURL`, verifies `<a>` attributes and `revokeObjectURL` called) in `Karamel.Web/wwwroot/js/exportBridge.test.js`

**Checkpoint**: `CsvExportHelper` helpers compile and JS bridge tests pass — user story implementation can begin.

---

## Phase 3: User Story 4 — Access Export Page Without Session (Priority: P1) 🎯 MVP Foundation

**Goal**: A standalone `/export` page loads without any session, shows a "Select Folder" button, invokes `exportBridge.scanDirectory()` on click, displays spinner during scan, shows song count on completion, and hides download buttons until scan completes.

**Independent Test**: Navigate to `/export` with no `?session=` parameter → page renders "Select Folder" button with no errors, no redirects, and no backend session created. Confirm download buttons are absent before scan.

- [X] T005 [US4] Create `Export.razor` with route `@page "/export"`, component-local state fields (`_songs`, `_isScanning`, `_scanComplete`, `_scanError`, `_filenamePattern = "%artist - %title"`), `IAsyncDisposable` pattern for `IJSObjectReference` module, `SelectFolder()` async handler (calls `exportBridge.scanDirectory`, sets `_isScanning`/`_scanComplete`/`_scanError`), spinner with "Scanning…" text during scan, song count display after scan, and conditional rendering of download-button section (hidden until `_scanComplete && !_isScanning`) in `Karamel.Web/Pages/Export.razor`
- [X] T006 [P] [US4] Create `Export.razor.css` with scoped styles for the select-folder button, spinner, song count, and download-buttons container following the STYLING_GUIDE caramel color palette in `Karamel.Web/Pages/Export.razor.css`
- [X] T007 [P] [US4] Write bUnit tests in `Karamel.Web.Tests/ExportPageTests.cs`: page renders without `?session=` parameter, "Select Folder" button is present, download buttons are absent before scan, spinner text "Scanning…" appears while `_isScanning = true`, error message appears when `_scanError` is set, `_scanComplete = true` renders the download-button section

**Checkpoint**: `/export` page is independently functional — operator can scan a directory and see song count. No download buttons yet.

---

## Phase 4: User Story 1 — Download Artist-Sorted Song List (Priority: P1)

**Goal**: Clicking "Download Artists" on a completed scan generates and downloads `artists.csv` sorted by Artist ascending (case-insensitive, digits/specials first), with header `Artist;Title`, UTF-8, semicolon-delimited.

**Independent Test**: Load `/export`, complete a scan, click "Download Artists", verify `artists.csv` has correct header, correct sort order, correct quoting for semicolons in fields, and UTF-8 encoding — independently of the other download buttons.

- [X] T008 [US1] Implement `GenerateArtistsCsv(IEnumerable<Song> songs)` — header `Artist;Title\n`, rows ordered by `(s.Artist ?? "").ToLowerInvariant()` using `StringComparer.Ordinal`, fields escaped via `EscapeCsvField` — in `Karamel.Web/Helpers/CsvExportHelper.cs`
- [X] T009 [US1] Add "Download Artists" button and `DownloadArtists()` handler (calls `CsvExportHelper.GenerateArtistsCsv(_songs)` then `exportBridge.triggerDownload(content, "artists.csv")`) inside the download-button section of `Karamel.Web/Pages/Export.razor`
- [X] T010 [P] [US1] Write xUnit tests for `GenerateArtistsCsv` in `Karamel.Web.Tests/ExportPageTests.cs`: header row present, alphabetical sort (digits/specials before A–Z, case-insensitive), fields with semicolons quoted per RFC 4180, empty song list yields header-only output, null Artist treated as empty string

**Checkpoint**: "Download Artists" is fully functional. `artists.csv` is verifiable independently.

---

## Phase 5: User Story 2 — Download Title-Sorted Song List (Priority: P1)

**Goal**: Clicking "Download Titles" generates and downloads `titles.csv` sorted by Title ascending, with header `Title;Artist`, same encoding and quoting rules as artists.csv.

**Independent Test**: Load `/export`, complete a scan, click "Download Titles", verify `titles.csv` has header `Title;Artist`, correct sort order, and that the file is independent of artists.csv.

- [X] T011 [P] [US2] Implement `GenerateTitlesCsv(IEnumerable<Song> songs)` — header `Title;Artist\n`, rows ordered by `(s.Title ?? "").ToLowerInvariant()` using `StringComparer.Ordinal`, fields escaped via `EscapeCsvField` — in `Karamel.Web/Helpers/CsvExportHelper.cs`
- [X] T012 [P] [US2] Add "Download Titles" button and `DownloadTitles()` handler (calls `CsvExportHelper.GenerateTitlesCsv(_songs)` then `exportBridge.triggerDownload(content, "titles.csv")`) inside the download-button section of `Karamel.Web/Pages/Export.razor`
- [X] T013 [P] [US2] Write xUnit tests for `GenerateTitlesCsv` in `Karamel.Web.Tests/ExportPageTests.cs`: header `Title;Artist` row present, alphabetical sort by Title (same rules as artists), semicolon-containing fields quoted, empty list yields header-only, null Title treated as empty string, identical titles with different artists appear consecutively

**Checkpoint**: "Download Titles" is fully functional and independently testable.

---

## Phase 6: User Story 3 — Download Duplicates Report (Priority: P2)

**Goal**: Clicking "Download Duplicates" generates and downloads `duplicates.csv` listing exact duplicate groups (same normalized Artist+Title) first, then likely duplicate groups (both Artist and Title within Levenshtein thresholds of 2 and 3 respectively after preprocessing), with columns `Artist;Title;FilePath`.

**Independent Test**: Load `/export` with a library containing known exact and near-duplicate entries, click "Download Duplicates", verify grouping order (exact first), consecutive rows within a group, header `Artist;Title;FilePath`, correct thresholds, and header-only output when no duplicates exist.

- [X] T014 [US3] Implement `FindExactDuplicateGroups(IEnumerable<Song> songs)` — key = `NormalizeForComparison(Artist) + "|" + NormalizeForComparison(Title)`, group by key using `Dictionary<string, List<Song>>`, return groups with ≥ 2 members — in `Karamel.Web/Helpers/CsvExportHelper.cs`
- [X] T015 [US3] Implement `FindLikelyDuplicateGroups(IEnumerable<Song> songs, IReadOnlyCollection<Guid> exactDuplicateSongIds)` — O(n²) pair comparison with four-step early-exit (length diff Artist > 2 skip, `OsaDistance` Artist > 2 skip, length diff Title > 3 skip, `OsaDistance` Title > 3 skip), Union-Find clustering, return groups with ≥ 2 members — in `Karamel.Web/Helpers/CsvExportHelper.cs`
- [X] T016 [US3] Implement `GenerateDuplicatesCsv(IEnumerable<Song> songs)` — calls `FindExactDuplicateGroups`, collects exact song IDs, calls `FindLikelyDuplicateGroups` excluding exact IDs, emits header `Artist;Title;FilePath\n`, then exact groups (all members consecutive), then likely groups (all members consecutive), using `EscapeCsvField` on all three columns — in `Karamel.Web/Helpers/CsvExportHelper.cs`
- [X] T017 [US3] Add "Download Duplicates" button and `DownloadDuplicates()` handler (calls `CsvExportHelper.GenerateDuplicatesCsv(_songs)` then `exportBridge.triggerDownload(content, "duplicates.csv")`) inside the download-button section of `Karamel.Web/Pages/Export.razor`
- [X] T018 [P] [US3] Write xUnit tests in `Karamel.Web.Tests/ExportPageTests.cs` for: `FindExactDuplicateGroups` (identical Artist+Title case-insensitive detected, non-duplicates not included, 3-way group correct), `FindLikelyDuplicateGroups` (within-threshold pair grouped, exceeding-threshold pair excluded, exact-duplicate songs excluded from candidates), `GenerateDuplicatesCsv` (exact groups before likely groups, header-only when no duplicates, FilePath column populated from `FullPath`)

**Checkpoint**: All three download buttons are functional. Full feature is independently testable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation — build cleanliness, test suite health, and quickstart scenario confirmation.

- [X] T019 Run `dotnet build` from solution root and verify zero warnings
- [X] T020 [P] Run `dotnet test Karamel.Web.Tests` and verify ≥ 260 tests pass (≤ 9 skipped by design)
- [X] T021 [P] Run `cd Karamel.Web\wwwroot; npm run test:run` from solution root and verify zero JavaScript test failures; navigate back with `cd ..\..`
- [X] T022 [P] Verify `NavMenu.razor` and all other navigation/layout files (`MainLayout.razor`, etc.) contain no link, route reference, or href pointing to `/export`; the page MUST be reachable only by direct URL entry (FR-002, SC-006)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — **BLOCKS all user stories**
- **US4 Page Shell (Phase 3)**: Depends on Phase 2 (T002 + T003)
- **US1 Artists (Phase 4)**: Depends on Phase 2 (T002 for `EscapeCsvField`) + Phase 3 (T005 for the button section in `Export.razor`)
- **US2 Titles (Phase 5)**: Depends on Phase 2 + Phase 3. Can run **in parallel with Phase 4** (different methods/buttons)
- **US3 Duplicates (Phase 6)**: Depends on Phase 2 (T002 for `NormalizeForComparison`, `OsaDistance`) + Phase 3. Can run after Phase 4 completes (all three methods are in the same `CsvExportHelper.cs` file — prevents same-file conflicts)
- **Polish (Phase 7)**: Depends on all user story phases completing

### User Story Dependencies

- **US4 (P1)**: Unblocked after Foundational — is the page scaffold, must precede US1/US2/US3 button additions
- **US1 (P1)**: Unblocked after US4 — no dependency on US2 or US3
- **US2 (P1)**: Unblocked after US4 — fully independent of US1 (different method, different CSV columns)
- **US3 (P2)**: Unblocked after US4 — builds on helpers from Foundational phase; no dependency on US1/US2

### Within Each User Story

- For US3: T014 and T015 implement independent static methods in the same file and should be done sequentially to avoid merge conflicts
- For US3: T016 (`GenerateDuplicatesCsv`) depends on T014 + T015 being complete
- For US3: T018 (tests) can be written in parallel with T014–T016 once `CsvExportHelper.cs` exists

---

## Parallel Opportunities

### Phase 2 (Foundational)

```
T002  Implement helpers in CsvExportHelper.cs       ─┐
T003  Create exportBridge.js                         ─┼─ run in parallel (different new files)
      T004  Write exportBridge.test.js               ─┘ (T004 after T003)
```

### Phase 3 (US4)

```
T005  Create Export.razor                            ─┐
T006  Create Export.razor.css                        ─┼─ run in parallel (different files)
T007  Write ExportPageTests.cs (page shell tests)    ─┘
```

### Phase 4 + Phase 5 (US1 + US2 — two developers)

```
Developer A                                Developer B
─────────────────────────────────          ──────────────────────────────────
T008  GenerateArtistsCsv (Helper)          T011  GenerateTitlesCsv (Helper)
T009  Download Artists button (Razor)      T012  Download Titles button (Razor)
T010  Tests for GenerateArtistsCsv         T013  Tests for GenerateTitlesCsv
```

*(US1 and US2 touch different methods in CsvExportHelper.cs and different buttons in Export.razor — no conflicts when coordinated.)*

### Phase 6 (US3)

```
T014  FindExactDuplicateGroups      )
T015  FindLikelyDuplicateGroups     ) sequential (same file)
T016  GenerateDuplicatesCsv         )
T017  Download Duplicates button    ─── after T016
T018  Tests for US3                 ─── [P] different file (ExportPageTests.cs)
```

---

## Implementation Strategy

### MVP Scope (User Stories US4 + US1)

1. Complete Phase 1 (baseline verify)
2. Complete Phase 2 (foundational helpers + JS bridge)
3. Complete Phase 3 (US4 — page shell without download buttons)
4. Complete Phase 4 (US1 — artists.csv download)
5. **STOP and VALIDATE**: Navigate to `/export`, scan a directory, download `artists.csv`, verify contents

### Incremental Delivery

1. Phases 1–2 → foundation ready
2. Phase 3 (US4) → page shell ✅ test independently
3. Phase 4 (US1) → `artists.csv` ✅ test independently
4. Phase 5 (US2) → `titles.csv` ✅ test independently
5. Phase 6 (US3) → `duplicates.csv` ✅ test independently
6. Phase 7 → final polish & green CI

### Performance Note

For US3 at 5,000 songs: O(n²) = 12.5M pairs. With four-step early-exit (~15 ops/pair average), estimated ~200 ms in WASM — well within the 5-second SC-005 budget. No optimization needed unless measured otherwise.

---

## Notes

- **No backend changes** — all logic is client-side in `Karamel.Web`
- **No new NuGet packages** — `CsvExportHelper.cs` implements its own OSA algorithm (~20 lines)
- **No Fluxor state** — `Export.razor` uses component-local state only (`_songs`, `_isScanning`, etc.)
- **No session validation** — `Export.razor` is explicitly exempt (documented architectural decision in plan.md)
- **Levenshtein thresholds**: Artist = 2, Title = 3 (see `ArtistLevenshteinThreshold` / `TitleLevenshteinThreshold` constants in `CsvExportHelper` — per FR-015)
- [P] tasks = different files, no incomplete dependencies
- [Story] label maps each task to its user story for traceability
- Run `dotnet build` after every phase to catch regressions early
