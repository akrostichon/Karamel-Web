# Research: Player Controls — Next & Previous Buttons

**Branch**: `009-player-next-prev`
**Phase**: 0 — Outline & Research
**Created**: 2026-03-15

## Decision 1: Icon choice for "Next" button

**Decision**: Replace `bi-stop-circle` with `bi-skip-end-circle` (Bootstrap Icons).

**Rationale**: `bi-skip-end-circle` is the standard skip-forward / track-next icon in Bootstrap Icons v1.x and maps directly to the "advance to next song" intent. `bi-stop-circle` carries the semantics of "stop all playback", which is misleading. `bi-skip-end` (non-circle) is also available; the circle variants are used consistently across the existing play/pause button (`bi-play-circle`, `bi-pause-circle`), so the circle variant maintains visual consistency.

**Alternatives considered**:
- `bi-skip-end` (no circle) — inconsistent with the other control buttons.
- `bi-fast-forward-circle` — implies "jump ahead" rather than "next track".
- `bi-arrow-right-circle` — generic directional, no skip semantics.

## Decision 2: Icon choice for "Previous" button

**Decision**: Use `bi-skip-start-circle` (Bootstrap Icons).

**Rationale**: `bi-skip-start-circle` is the mirror of `bi-skip-end-circle` and represents "go back to start / previous track" in standard media player vocabulary. It fits the "restart current song" semantics adequately. Matches the circle style of all other control buttons.

**Alternatives considered**:
- `bi-skip-start` (no circle) — inconsistent with existing controls.
- `bi-arrow-counterclockwise` — conveys "undo" or "replay" but lacks media-player familiarity.
- `bi-repeat-1` — implies looping, not a one-shot restart.

## Decision 3: New JS function — `restartPlayback()` vs reusing `stopPlayback()`

**Decision**: Add a new exported function `restartPlayback()` to `player.js`.

**Rationale**: `stopPlayback()` pauses playback AND resets `currentTime = 0`. For "Previous", we want to seek to 0 while **resuming** playback (always plays after previous, per Acceptance Scenario 3). Reusing `stopPlayback()` would pause the song. A dedicated `restartPlayback()` function:
- Sets `audioElement.currentTime = 0` (CDG mode)
- Sets `videoElement.currentTime = 0` (video mode)
- Calls `.play()` on the media element to ensure playback starts
- For CDG mode: the existing `seeked` event listener already calls `renderFrame()`, so the CDG display re-renders automatically from position 0. The animation loop is already running if playing; `.play()` ensures it resumes if paused.

**Alternatives considered**:
- Combining with `stopPlayback()` via a parameter flag — adds coupling and complexity.
- InvokeAsync on `seekTo(0)` — would require a new `seekTo` function anyway, with less descriptive intent.

## Decision 4: Previous button always starts playback (not maintains pause state)

**Decision**: `restartPlayback()` always calls `.play()` — even if the player was paused at the time of clicking Previous.

**Rationale**: Acceptance Scenario 3 of the spec explicitly states: "song position resets to the beginning and **playback resumes from the start**" when paused. Karaoke use case: a KJ clicking Previous has intent to replay the song; starting it automatically is the expected and most useful behavior. This aligns with FR-006's "resume or maintain current play/pause state" — when paused we interpret "resume" as the appropriate action.

**Alternatives considered**:
- Maintain pause state (seek only, no auto-play) — contradicts Acceptance Scenario 3 and less useful for KJ workflow.

## Decision 5: No backend changes required

**Decision**: This feature requires zero backend changes.

**Rationale**: The "Previous" action is a browser-local media seek operation. No playlist state changes on the backend, no new SignalR hub methods, no new REST endpoints. The `playerModule.InvokeVoidAsync("restartPlayback")` call is local JS interop only.

## Decision 6: No new Fluxor actions or state changes

**Decision**: `RestartSong()` in C# sets `isPlaying = true` directly (same pattern as `TogglePlayPause`) and does not dispatch a Fluxor action.

**Rationale**: Song restart is a transient playback event, not a domain state change. `isPlaying` is a local component field that drives button icon rendering. Dispatching a Fluxor action would add unnecessary complexity for a purely local UI concern. The pattern matches `TogglePlayPause()` which also sets `isPlaying` directly without dispatching actions.

## Decision 7: Guard for null playerModule

**Decision**: `RestartSong()` returns early if `playerModule == null`, identical to the guard in `TogglePlayPause()`.

**Rationale**: `playerModule` is null before the JS module is dynamically imported. This guard is already the standard defensive pattern in PlayerView and must be applied consistently.

## Key file locations confirmed via codebase research

| File | Relevant lines | Change required |
|------|---------------|-----------------|
| `Karamel.Web/Pages/PlayerView.razor` | L94–99 (button markup) | Add Previous button, rename Stop → Next |
| `Karamel.Web/Pages/PlayerView.razor` | L606 (`StopPlayback`) | Rename to `StopPlayback` → keep handler, only button label/icon change |
| `Karamel.Web/wwwroot/js/player.js` | L268 (`stopPlayback`) | No change; new `restartPlayback()` added alongside |
| `Karamel.Web.Tests/PlayerViewTests.cs` | L166 (button count) | Update count 3 → 4 |
| `Karamel.Web.Tests/PlayerViewTests.cs` | L504 (button index [1]) | Update index [1] → [2] for Next button |
| `Karamel.Web/wwwroot/js/player.test.js` | New describe block | Add `restartPlayback` tests |
