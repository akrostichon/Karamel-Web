# Feature Specification: Song Duration Display

**Feature Branch**: `001-song-duration-display`  
**Created**: 2026-03-01  
**Status**: Draft  
**Input**: User description: "Parse song duration during library scan; show duration in UpNextList component (SingerView) and Playlist Manager queue rows; add non-interactive progress bar to PlayerView controls overlay on hover."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Duration Captured During Library Scan (Priority: P1)

When a host opens a folder (or ZIP) containing karaoke songs, the system reads each MP3 and MP4 file's duration as part of the existing metadata scan and stores it alongside the song's title and artist.

**Why this priority**: Duration is foundational data all display stories depend on it. Without this, nothing can be shown anywhere. It is the minimal viable piece and can be demonstrated independently by inspecting the library after a scan.

**Independent Test**: Scan a directory of MP3/MP4 files and verify that each song in the loaded library has a non-zero, plausible duration value.

**Acceptance Scenarios**:

1. **Given** a host selects a folder containing MP3 files, **When** the library scan completes, **Then** every scanned song has a duration value in whole seconds that is greater than zero.
2. **Given** a folder contains MP4 video files, **When** the library scan completes, **Then** MP4 songs also have a duration value captured.
3. **Given** a file whose duration cannot be determined (corrupt header), **When** scanning, **Then** the song is still added to the library with a duration of zero (not skipped).
4. **Given** a ZIP archive containing MP3 files, **When** the library scan completes, **Then** songs extracted from the ZIP also carry duration values.

---

### User Story 2 - Duration Shown in UpNextList Component (Priority: P2)

The **UpNextList** component (embedded in the Singer View sidebar) shows the currently playing song and the queued songs. Duration should be displayed for both the "Now Playing" entry and every queued item, so singers and the host can see at a glance how long each song is.

**Why this priority**: Singers use this view directly from their phones to decide how soon they can add themselves to the queue. Duration helps them plan. This is the primary user-facing queue display.

**Independent Test**: Open the Singer View with at least one song queued; verify that each item in the UpNextList shows a duration value.

**Acceptance Scenarios**:

1. **Given** the Singer View is open and a song is "Now Playing", **When** the UpNextList renders, **Then** the now-playing card shows the song's duration right-aligned in `m:ss` format.
2. **Given** the Singer View is open with songs in the queue (UpNext / Queued status), **When** the UpNextList renders each row, **Then** the duration is displayed right-aligned at the end of the row in `m:ss` format.
3. **Given** a song has a duration of zero or absent, **When** it appears in the UpNextList, **Then** the duration field is hidden (not shown as `0:00`).
4. **Given** the queue changes (song added or removed), **When** the UpNextList re-renders, **Then** the duration values update correctly for all rows.

---

### User Story 3 - Duration Shown in Playlist Manager Queue Rows (Priority: P3)

The Playlist Manager page has its own inline queue list so the host can reorder and manage songs. Duration should be visible for each item so the host can estimate total event time.

**Why this priority**: Useful operational information for the host managing the event, but less time-critical than the Singer View display.

**Independent Test**: Open the Playlist Manager with several songs queued; verify each queue row shows the song duration.

**Acceptance Scenarios**:

1. **Given** the Playlist Manager is open with songs in the queue, **When** any playlist item is displayed, **Then** each row shows the song duration right-aligned in `m:ss` format.
2. **Given** a song has no duration data (duration = 0), **When** it appears in the queue, **Then** the duration cell is empty or shows a dash (`—`), not `0:00`.
3. **Given** a singer adds a song from a remote device, **When** the Playlist Manager updates, **Then** the duration for the newly added song is also visible.

---

### User Story 4 - Song Progress Bar Visible on Player Hover (Priority: P2)

When hovering over the lower area of the Player View (where the playback controls appear), a non-interactive progress bar is shown above the buttons. It fills left-to-right to indicate how far through the current song the playback is, matching the behaviour of mainstream media players (VLC, Spotify, YouTube).

**Why this priority**: Without visual playback progress the host has no at-a-glance sense of how far through a song they are. This is a standard expectation for any media player interface.

**Independent Test**: Play a song in the Player View, hover over the lower control area, verify that a progress bar appears and advances as playback proceeds.

**Acceptance Scenarios**:

1. **Given** a song is playing and the user hovers over the lower area of the Player View, **When** the controls overlay appears, **Then** a progress bar is visible above the control buttons, showing the proportion of the song elapsed.
2. **Given** the progress bar is visible, **When** the user clicks or drags on it, **Then** nothing happens — the bar is display-only and does not seek.
3. **Given** the song advances in playback, **When** the progress bar is visible, **Then** it updates smoothly (at most every second) to reflect the current position.
4. **Given** no song duration is available (duration = 0), **When** the overlay is shown, **Then** the progress bar is hidden entirely.
5. **Given** the user moves the mouse away from the lower area, **When** the controls overlay hides, **Then** the progress bar also disappears.

---

### Edge Cases

- What happens when a song file has an unusually large duration (e.g., >1 hour)?  Display as `h:mm:ss` (e.g., `1:02:30`).
- What if duration cannot be read because the browser API is unavailable?  Duration defaults to zero and is silently omitted from the display.
- What if the library is re-scanned?  Durations are re-captured and overwrite previous values.
- What if a singer selects a song on a remote device (phone)?  Duration was captured at scan time and stored with the song; the remote device receives it via the backend API.
- What happens on the progress bar when the song is paused?  The bar freezes at the current position until playback resumes.
- What about CDG files?  CDG files carry no audio duration; duration is taken from the paired MP3.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST extract the duration (in seconds) from each MP3 and MP4 file during the library scan, in the same scan pass that reads artist and title metadata.
- **FR-002**: The extracted duration MUST be stored as part of the song metadata and persisted to the backend so it is available to all session participants including remote devices.
- **FR-003**: The `UpNextList` component MUST display the duration right-aligned on each row (Now Playing card and all queue items) in `m:ss` format. If duration is zero or absent the field MUST be omitted entirely.
- **FR-004**: Each queue row in the Playlist Manager page MUST display the song duration right-aligned. A zero or absent duration MUST be displayed as a dash (`—`) or be hidden.
- **FR-005**: Duration display MUST format durations of 60 minutes or longer as `h:mm:ss`.
- **FR-006**: Duration extraction MUST NOT cause the library scan to fail; files where duration cannot be determined MUST still be added to the library with duration zero.
- **FR-007**: Duration data MUST travel from the main tab to the backend on upload and be returned to any device via the library API. It MUST NOT rely on `BroadcastChannel` or `sessionStorage` for remote devices.
- **FR-008**: Duration values MUST be displayed right-aligned at the end of each row in both the `UpNextList` component and the Playlist Manager queue list, without a column header label.
- **FR-009**: The Player View controls overlay MUST include a non-interactive, read-only progress bar above the playback buttons. The bar MUST fill left-to-right to represent the elapsed fraction of the current song. It MUST NOT respond to click or drag input. It MUST be hidden when the song's duration is unavailable (zero).

### Key Entities

- **Song**: Gains a `DurationSeconds` (integer, seconds) field. Zero means unknown/unavailable.
- **SongUploadDto / SongDto**: Must include `durationSeconds` so the value round-trips between client and backend.
- **PlaylistItemDto**: Must carry `durationSeconds` for display in queue views (derived from the song).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After scanning a library of 100+ songs, 100% of MP3 and MP4 files with valid headers have a non-zero duration value stored.
- **SC-002**: Every row in the `UpNextList` component and in the Playlist Manager queue shows the song duration within the same render cycle as the artist and title — no additional loading step required.
- **SC-003**: A singer using the Singer View on a phone can see the duration of every queued song without taking any additional action.
- **SC-004**: Opening the Singer View or Playlist Manager on a remote device (phone via QR code) shows the same durations as the host's view.
- **SC-006**: When hovering over the Player View, the progress bar visually reflects the song position and updates at least once per second.
- **SC-005**: A corrupt or unreadable file does not prevent the remaining library from loading (zero scan failures caused by duration errors).

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any  must be justified before the spec is approved.

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: Duration is stored in the backend and delivered via API, so phones/tablets opened via QR code receive duration data without any filesystem access.
- [x] **Backend as source of truth**: Duration is part of the SongDto returned by the library API; non-main tabs fetch it from the backend.
- [x] **Session ID from backend**: This feature does not introduce new session IDs.
- [x] **Session parameter validated**: No new pages are introduced by this feature.

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: Duration is a numeric value; no file paths are included in uploaded metadata.
- [x] **Minimal data**: An integer duration field is minimal and directly required for the feature.
- [x] **Consent-gated telemetry**: No new Application Insights events are required beyond existing patterns.
- [x] **No sensitive logging**: Duration values contain no personal or sensitive information.

## Assumptions

- Duration extraction happens in the browser via the Web Audio API or the `<audio>`/`<video>` element's `duration` property, since the main tab already holds the file handle. No server-side media processing is required.
- `DurationSeconds` is stored as an integer (whole seconds). Sub-second precision is not needed for display.
- The existing `MetadataJson` field or a new dedicated `DurationSeconds` column is used for persistence  the exact approach is a planning-phase decision.
- CDG files do not carry audio duration; their paired MP3 is the duration source.
