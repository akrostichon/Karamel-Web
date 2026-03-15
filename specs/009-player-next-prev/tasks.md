# Tasks: Player Controls — Next & Previous Buttons

**Input**: Design documents from `/specs/009-player-next-prev/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, quickstart.md ✓

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to ([US1] or [US2])
- Include exact file paths in descriptions

## Phase 1: Setup & Phase 2: Foundational — Not Applicable

This feature is frontend-only with no new infrastructure, DTOs, database schema, or external dependencies. All work is confined to 3 existing source files and 2 existing test files.

---

## Phase 3: User Story 1 — Rename Stop to Next (Priority: P1) 🎯 MVP

**Goal**: The KJ sees a "Next" button with a recognisable skip-forward icon (`bi-skip-end-circle`) instead of the current "Stop" button. The click handler behaviour — advancing the playlist — is unchanged.

**Independent Test**: Open PlayerView with a song loaded; verify the control row shows a skip-end circle icon and that clicking it still advances the playlist exactly as the old Stop button did.

- [X] T001 [P] [US1] Update Stop button — rename `StopPlayback` → `NextSong` (method definition + `@onclick` handler) and change icon `bi-stop-circle` → `bi-skip-end-circle` in `Karamel.Web/Pages/PlayerView.razor`
- [X] T002 [P] [US1] Rename test method `Component_StopButton` → `Component_NextButton_NavigatesToNextSongView` in `Karamel.Web.Tests/PlayerViewTests.cs`

**Checkpoint**: PlayerView displays `bi-skip-end-circle` on the Next button; all existing C# tests pass; `StopPlayback` name no longer exists in the codebase.

---

## Phase 4: User Story 2 — Add Previous Button (Priority: P2)

**Goal**: A "Previous" button (`bi-skip-start-circle`) appears to the left of the Play/Pause button. Clicking it seeks the current song back to position 0 and resumes playback — it never navigates to a different playlist entry.

**Independent Test**: Start a song, let it play for 10+ seconds, click Previous — playback restarts from 0:00 immediately. Pause the song mid-way, click Previous — position resets to 0:00 and playback automatically resumes.

- [X] T003 [P] [US2] Add `export function restartPlayback()` after `stopPlayback` in `Karamel.Web/wwwroot/js/player.js` (per plan Step 1: set `currentTime = 0` and call `.play()` for both CDG and video modes)
- [X] T004 [US2] Add `describe('restartPlayback')` test block to `Karamel.Web/wwwroot/js/player.test.js` (4 tests: CDG while playing, CDG while paused, video mode, no-op when player uninitialized) — depends on T003
- [X] T005 [US2] Add `private async Task RestartSong()` method (null guard + `isPlaying = true` pattern) to `Karamel.Web/Pages/PlayerView.razor`
- [X] T006 [US2] Add Previous button (`bi-skip-start-circle`, `@onclick="RestartSong"`) before the Play/Pause button in `Karamel.Web/Pages/PlayerView.razor`
- [X] T007 [US2] Update `Karamel.Web.Tests/PlayerViewTests.cs`: change button count assertion `3` → `4`, update Next button index `[1]` → `[2]`, add skipped test `Component_PreviousButton_RestartsCurrentSong` (`[Fact(Skip = "JSInterop mocking limitations")]`)

**Checkpoint**: PlayerView shows four buttons in `[Previous] [Play/Pause] [Next] [Fullscreen]` order; all C# tests pass with button count 4; all JS `restartPlayback` tests pass.

---

## Phase 5: Polish & Validation

**Purpose**: Confirm all changes integrate cleanly and no regressions were introduced.

- [X] T008 Run `dotnet test Karamel.Web.Tests` (expect 0 new failures, 4-button count test passes) and `cd Karamel.Web\wwwroot; npm run test:run; cd ..\..` (expect `restartPlayback` describe block passes with zero failures)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 3 (US1)**: No upstream dependencies — can start immediately
- **Phase 4 (US2)**: T003 and T004 can start immediately and in parallel with Phase 3; T005–T007 depend on T003 being complete
- **Phase 5 (Polish)**: Depends on all Phase 3 and Phase 4 tasks being complete

### User Story Dependencies

- **US1 (P1)**: T001 and T002 are fully independent of each other (different files) — both marked [P]
- **US2 (P2)**: T003 and T004 are independent of US1 tasks (different files); T005/T006/T007 depend on T003

### Dependency Graph

```
T001 [US1] ─── (no downstream)
T002 [US1] ─── (no downstream)

T003 [US2] ──► T004 [US2]
          └──► T005 [US2] ──► T006 [US2] ──► T007 [US2]
                                                        └──► T008
```

### Parallel Opportunities

| Tasks       | Can run simultaneously?                         |
|-------------|--------------------------------------------------|
| T001 + T002 | ✅ Yes — `PlayerView.razor` vs `PlayerViewTests.cs` |
| T001 + T003 | ✅ Yes — `PlayerView.razor` vs `player.js`          |
| T002 + T003 | ✅ Yes — different files                            |
| T003 + T004 | ⚠️ T004 can be stubbed before T003, but requires T003 for final pass |
| T005 + T006 | ❌ No — both modify `PlayerView.razor` (do sequentially) |

---

## Implementation Strategy

**MVP Scope**: Complete US1 (Phase 3, T001–T002) first — 2-task change with zero risk and immediate KJ clarity payoff.

**Recommended US2 Execution Order** (sequential on one track):

1. **T003** — Add `restartPlayback()` to `player.js` (JS foundation for all US2)
2. **T004** — Add JS tests (validate T003 before touching Blazor)
3. **T005** — Add `RestartSong()` C# method to `PlayerView.razor`
4. **T006** — Add Previous button markup to `PlayerView.razor` (same file, sequential after T005)
5. **T007** — Update `PlayerViewTests.cs` (finalise button count, index, new skip test)

**Suggested MVP delivery**: Ship US1 alone (T001–T002) as an immediate patch, then US2 (T003–T007) as the follow-up increment.
