# Tasks: Song Duration Display

**Input**: Design documents from `/specs/001-song-duration-display/`  
**Feature Branch**: `001-song-duration-display`

> **Note on existing code**: There is **no existing song duration extraction** in the codebase. The `duration` references in `fileAccess.js` are all performance telemetry (`durationMs` in milliseconds for `trackLoadTelemetry`). All three builder functions (`buildDirectorySong`, `buildVideoSong`, `buildZipSong`) need new `extractDuration` wiring.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependency on in-progress tasks)
- **[Story]**: Which user story this task belongs to

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core model, formatter, and backend contract changes that every user story depends on.

**⚠️ CRITICAL**: All Phase 2 tasks must be complete before any user story work begins.

- [X] T001 Add `DurationSeconds int` property (default `0`) to `Karamel.Web/Models/Song.cs`
- [X] T002 [P] Create `DurationFormatter.Format(int seconds)` static helper in `Karamel.Web/Helpers/DurationFormatter.cs` — returns `null` for `≤0`, `m:ss` for `<1h`, `h:mm:ss` for `≥1h`
- [X] T003 [P] Add `DurationSeconds int DurationSeconds = 0` default parameter to `PlaylistItemDto` record in `Karamel.Web/Contracts/PlaylistItemDto.cs`
- [X] T004 Add private `ParseDuration(string? metadataJson)` helper and populate `durationSeconds` in all item projections in `Karamel.Backend/Hubs/PlaylistHub.cs`
- [X] T005 [P] Add xUnit tests for `DurationFormatter.Format()` (zero → null, 215 → `"3:35"`, 3661 → `"1:01:01"`) in new `Karamel.Web.Tests/DurationFormatterTests.cs`

---

## Phase 3: User Story 1 — Duration Captured During Library Scan (P1)

**Goal**: Every scanned MP3, MP4, and ZIP-origin song carries a non-zero `durationSeconds` value after the library scan completes.

**Independent test**: Scan a folder with ≥1 MP3 and ≥1 MP4; confirm every song in the loaded library has `DurationSeconds > 0`.

- [X] T006 [US1] Add private `extractDuration(fileOrBlob)` async helper to `Karamel.Web/wwwroot/js/fileAccess.js` — uses temporary `<audio>` element for MP3, `<video>` for MP4; returns `Math.round(el.duration)` or `0` on error/NaN/Infinity; revokes the object URL after reading
- [X] T007 [US1] Wire `durationSeconds: await extractDuration(fileObj)` into `buildDirectorySong`, `buildVideoSong` (use `<video>` element), and `buildZipSong` (pass the ArrayBuffer already read for metadata) in `Karamel.Web/wwwroot/js/fileAccess.js`
- [X] T008 [P] [US1] Update `ConvertSongToUploadDto` to always serialise `durationSeconds` into MetadataJson (merge with video metadata when applicable; omit JSON entirely only if `DurationSeconds == 0` AND not a video song) in `Karamel.Web/Contracts/SongDto.cs`
- [X] T009 [P] [US1] Update `ConvertJsonToSong` to parse `durationSeconds` from MetadataJson and set `DurationSeconds` on the returned `Song` in `Karamel.Web/Contracts/SongDto.cs`
- [X] T010 [P] [US1] Add Vitest tests for `extractDuration` in `Karamel.Web/wwwroot/js/fileAccess.test.js` — mock `document.createElement`; verify correct seconds for a fake audio blob, `0` for error event, `0` for `NaN` duration
- [X] T011 [P] [US1] Add xUnit tests for `ConvertSongToUploadDto`/`ConvertJsonToSong` round-trip with `DurationSeconds=175` in `Karamel.Web.Tests/SongDtoConverterTests.cs`

---

## Phase 4: User Story 2 — Duration in UpNextList Component (P2)

**Goal**: Every row in the `UpNextList` component shows the song's duration right-aligned in `m:ss` format; rows with `DurationSeconds == 0` show nothing.

**Independent test**: Open Singer View with ≥1 queued song; verify each UpNextList row shows a duration value.

- [ ] T012 [US2] Add right-aligned duration display to each row (Now Playing card and queue items) in `Karamel.Web/Components/UpNextList.razor` using `DurationFormatter.Format()`; add `.up-next-song-duration` flex-shrink scoped style in `Karamel.Web/Components/UpNextList.razor.css`
- [ ] T013 [P] [US2] Add bUnit tests for `UpNextList` duration rendering (song with `DurationSeconds=215` → `"3:35"` visible; song with `DurationSeconds=0` → duration element absent) in new `Karamel.Web.Tests/UpNextListTests.cs`

---

## Phase 5: User Story 4 — Song Progress Bar in Player Hover Overlay (P2)

**Goal**: Hovering over the Player View shows a non-interactive progress bar above the control buttons; bar fills left-to-right as playback progresses; hidden when `DurationSeconds == 0`.

**Independent test**: Play a song, hover over the lower player area; verify the progress bar appears and advances every ~1 s.

- [ ] T014 [US4] Add `export function getPlaybackPosition()` to `Karamel.Web/wwwroot/js/player.js` — returns `audioElement.currentTime` in CDG mode, `videoElement.currentTime` in video mode, `0` otherwise
- [ ] T015 [US4] Add progress bar markup (`.playback-progress-bar-container` + `.playback-progress-bar-fill` with `style="width:@percent%"`) above the controls buttons, plus `_progressTimer`, `playbackProgressPercent`, `playbackDurationSeconds` fields, and 1 s `PollPlaybackProgress()` in `Karamel.Web/Pages/PlayerView.razor`; start/stop timer in `ShowControls()`/`HideControls()`
- [ ] T016 [P] [US4] Add `.playback-progress-bar-container` and `.playback-progress-bar-fill` CSS (4 px height, `pointer-events: none`, caramel `--k-primary` fill) to `Karamel.Web/Pages/PlayerView.razor.css`
- [ ] T017 [P] [US4] Add Vitest test for `getPlaybackPosition()` in `Karamel.Web/wwwroot/js/player.test.js` — returns `audioElement.currentTime` in CDG mode, `videoElement.currentTime` in video mode, `0` with no active element
- [ ] T018 [P] [US4] Add bUnit test for PlayerView progress bar presence (`DurationSeconds > 0` + `showControls=true` → bar present; `DurationSeconds=0` → bar absent) in `Karamel.Web.Tests/PlayerViewTests.cs`

---

## Phase 6: User Story 3 — Duration in Playlist Manager Queue Rows (P3)

**Goal**: Every queue row in the Playlist Manager page shows the song's duration right-aligned; rows with `DurationSeconds == 0` show `—` or nothing (not `0:00`).

**Independent test**: Open Playlist Manager with ≥2 songs queued; verify each row shows a duration.

- [ ] T019 [US3] Add right-aligned duration cell to each queue row in `Karamel.Web/Pages/Playlist.razor` using `DurationFormatter.Format()` (show `—` when null); add scoped duration column style to `Karamel.Web/Pages/Playlist.razor.css`
- [ ] T020 [P] [US3] Add bUnit test for Playlist Manager queue duration rendering (song with `DurationSeconds=180` → `"3:00"` visible; song with `DurationSeconds=0` → shows `—` or no duration cell) in `Karamel.Web.Tests/PlaylistPageTests.cs`

---

## Final Phase: Polish & Validation

- [ ] T021 [P] Run `dotnet test Karamel.Web.Tests` and confirm ≥ 197 passing (9 skipped expected); fix any regressions
- [ ] T022 [P] Run `cd Karamel.Web/wwwroot; npm run test:run` and confirm zero JS test failures; fix any regressions
- [ ] T023 Request user to run `dotnet test Karamel.Backend.Tests -v minimal` (~40 s) and confirm PlaylistHub projection tests pass

---

## Summary

| Metric | Count |
|--------|-------|
| Total tasks | 23 |
| Phase 2 (Foundation) | 5 |
| US1 (P1) tasks | 6 |
| US2 (P2) tasks | 2 |
| US4 (P2) tasks | 5 |
| US3 (P3) tasks | 2 |
| Polish tasks | 3 |
| Parallel opportunities | 13 tasks marked `[P]` |

## Dependency Graph

```
Phase 2 (T001–T005)
    └── Phase 3 / US1 (T006–T011)
            ├── Phase 4 / US2 (T012–T013)   [depends on T001, T002, T003, T004]
            ├── Phase 5 / US4 (T014–T018)   [depends on T001]
            └── Phase 6 / US3 (T019–T020)   [depends on T001, T002, T003, T004]
```

US2, US4, and US3 all depend on Phase 2 foundation tasks but are **independent of each other** — they can be worked in any order after US1 is complete.

## Parallel Execution Examples

**Within Phase 2**:
- T002 (DurationFormatter.cs) ‖ T003 (PlaylistItemDto.cs) — different files

**Within Phase 3 (after T007)**:
- T008 (ConvertSongToUploadDto) ‖ T009 (ConvertJsonToSong) — same file, different methods; implement together or sequentially
- T010 (Vitest extractDuration) ‖ T011 (xUnit round-trip) — different test files

**Within Phase 5 (after T015)**:
- T016 (CSS) ‖ T017 (Vitest) ‖ T018 (bUnit) — all different files

## Suggested MVP Scope

**Phase 2 + Phase 3** (T001–T011): Duration is captured, stored in the backend, and available to all devices. This is a complete, demonstrable increment — the library inspector (Fluxor DevTools or console) confirms every song has `DurationSeconds > 0` after scan.
