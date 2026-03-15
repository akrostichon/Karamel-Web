# Feature Specification: Player Controls — Next & Previous Buttons

**Feature Branch**: `009-player-next-prev`
**Created**: 2026-03-15
**Status**: Draft
**Input**: User description: "REQ-1: Rename Stop Button to Next Button in PlayerView. REQ-2: Add Previous Button in PlayerView."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rename Stop to Next (Priority: P1)

The KJ (karaoke jockey) operating the player sees a "Next" button instead of the current "Stop" button in the PlayerView. The button carries a recognizable "skip-forward" icon consistent with standard media player conventions. The underlying behavior — advancing to the next song in the queue — is unchanged.

**Why this priority**: This is a pure label and icon rename that clarifies the button's actual function. It directly reduces operator confusion and is a prerequisite for a coherent three-button control row alongside the new Previous button.

**Independent Test**: Can be fully tested by opening the PlayerView with a song loaded and verifying the button label reads "Next" and that clicking it still advances the playlist as before.

**Acceptance Scenarios**:

1. **Given** the PlayerView is displayed with a song loaded, **When** the operator views the player controls, **Then** the button previously labeled "Stop" is now labeled "Next" and shows a skip-forward icon.
2. **Given** the PlayerView is displayed, **When** the operator clicks the "Next" button, **Then** the current song ends and the playlist advances to the next song — identical to the previous "Stop" behavior.

---

### User Story 2 - Add Previous Button (Priority: P2)

The KJ operating the player can click a "Previous" button to restart the currently playing song from the beginning. This button is positioned to the left of the Pause/Play button in the control row, using a standard "skip-back" icon.

**Why this priority**: Restarting a song is a common karaoke operation (singer missed the intro). This is separate from REQ-1 and can be developed and tested independently.

**Independent Test**: Can be fully tested by starting a song, letting it play for a few seconds, clicking "Previous", and confirming playback resumes from the beginning (time position returns to 0:00).

**Acceptance Scenarios**:

1. **Given** a song is currently playing, **When** the operator clicks the "Previous" button, **Then** the song restarts from the beginning (position 0) and continues playing.
2. **Given** a song is currently playing and has been playing for several minutes, **When** the operator clicks "Previous" multiple times in succession, **Then** each click restarts the song from the beginning — the song never navigates to a different queue entry.
3. **Given** a song is currently paused, **When** the operator clicks the "Previous" button, **Then** the song position resets to the beginning and playback resumes from the start.
4. **Given** a song is at position 0:00 (just started), **When** the operator clicks "Previous", **Then** the song simply restarts from the beginning (no error, no navigation change).

---

### Edge Cases

- What happens when "Previous" is clicked while no song is loaded? The button should either be disabled or have no visible effect — it must not cause an error.
- What happens when "Previous" is clicked and the song is at exactly position 0? The seek silently succeeds (seek to 0 from 0 is a no-op or a harmless restart).
- What happens when "Next" is clicked on the last song in the queue? Existing behavior is preserved — this spec does not change end-of-queue logic.
- What happens when the "Previous" button is clicked from a non-main-tab context? The PlayerView is restricted to the main tab only (via `IsMainTab` check); the button is not accessible from remote devices.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The "Stop" button label in PlayerView MUST be changed to "Next".
- **FR-002**: The "Next" button MUST display a skip-forward icon (e.g., a right-pointing double-chevron or skip-forward symbol consistent with standard media player conventions).
- **FR-003**: The "Next" button MUST retain the exact same click-handler behavior as the former "Stop" button (advancing the playlist to the next song).
- **FR-004**: A "Previous" button MUST be added to the PlayerView player controls row, positioned to the left of the Pause/Play button.
- **FR-005**: The "Previous" button MUST display a skip-back icon consistent with standard media player conventions.
- **FR-006**: Clicking the "Previous" button MUST seek the currently playing media back to position 0 (the beginning) and resume playback — Previous always starts playback, even if the player was paused at the time of the click.
- **FR-007**: The "Previous" button MUST NOT navigate to any prior entry in the playlist queue — it only restarts the current song.
- **FR-008**: Clicking "Previous" multiple times MUST always restart the current song from the beginning — no cumulative or alternative behavior.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After the change, 100% of operator test sessions identify the "Next" label/icon as "advance to next song" without prompting (eliminates the ambiguity of the "Stop" label).
- **SC-002**: Clicking "Previous" returns the playback position to 0:00 within 0.5 seconds in all cases.
- **SC-003**: No regression in existing "Next" (formerly "Stop") behavior — 100% of existing playlist-advance test scenarios continue to pass.
- **SC-004**: All three player control buttons (Previous, Pause/Play, Next) are visible and usable at the standard screen sizes used during karaoke sessions (desktop browser, full-screen mode).

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any ❌ must be justified before the spec is approved.
> Full principles: [Karamel-Web Constitution](.specify/memory/constitution.md)

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: The PlayerView is main-tab-only (gated by `IsMainTab`). These buttons are only visible and actionable on the host device — no remote-device impact.
- [x] **Backend as source of truth**: No library or playlist data is introduced by this feature. Existing data flows are unchanged.
- [x] **Session ID from backend**: No new session ID usage. Feature makes no changes to session management.
- [x] **Session parameter validated**: No new pages are introduced. PlayerView already validates `?session=`.

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: The "Previous" action is a local seek operation only — no file path data is transmitted.
- [x] **Minimal data**: No new personal or user data is introduced.
- [x] **Consent-gated telemetry**: No new telemetry events are introduced by this feature.
- [x] **No sensitive logging**: No new logging is introduced.
