# Feature Specification: Admin Session Control

**Feature Branch**: `001-admin-session-control`  
**Created**: 2026-02-21  
**Status**: Draft  
**Input**: User description:  
> Extend the karaoke app so that the host (the “admin”) can manage the running session in several ways without disrupting singers or requiring page navigation. Controls are admin‑only, accessible via a special link delivered from the main tab, and operate in‑process via SignalR commands. Settings do not persist across sessions.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 – Session lifecycle commands (Priority: P1)

The admin needs to be able to pause, resume and advance the queue from any open tab/device without navigating away or interfering with singers.

**Why this priority**:  
Core control mechanism; without it the feature delivers no value.

**Independent Test**:  
Open two tabs as admin and one as singer. Issue pause, resume and advance from the admin tabs and verify all tabs respond correctly (player stops, queue updates ignored, next song plays when resumed, advance works from any page).

**Acceptance Scenarios**:

1. **Given** an active session, **when** the admin clicks “Pause”, **then** the client sets a paused flag, sends a SignalR broadcast (`ReceiveSessionPaused`) and all connected tabs suppress automatic progression until a Resume command is received.
2. **Given** the session is paused and a singer adds songs, **when** the admin clicks “Resume”, **then** progression resumes normally and queued items are honoured.
3. **Given** the session is in any state, **when** the admin clicks “Next”, **then** the same `AdvanceToNextSongAsync` call the player uses is invoked and the playlist advances immediately on all tabs.
4. **Given** a non‑admin tab receives a pause/resume event, **then** it adjusts internal state but shows no notification or UI change (ignore silently).

---

### User Story 2 – Runtime configuration (Priority: P1)

The admin can toggle session options on the fly; changes affect all open tabs/devices and are included in the session DTO for new tabs.

**Why this priority**:  
Provides necessary flexibility for managing a live session; without it, the “admin” role has little impact.

**Independent Test**:  
Modify each of the four settings in one admin tab and open a new tab or refresh a second tab; verify values match and behaviour (e.g. singer name required blocks adds, reordering disabled, pause‑between‑songs respected, theme changes clients).

**Acceptance Scenarios**:

1. **Given** a running session, **when** the admin toggles “Singer name required”, **then** all tabs enforce the new rule immediately and the session DTO returned by the backend shows the updated flag.
2. **Given** the admin changes “Allow reorder”, **then** existing Playlist pages remove/restore drag handles in real time.
3. **Given** a non‑negative integer is entered for “Pause between songs”, **when** the admin saves, **then** subsequent advances (auto or manual) wait the configured seconds.
4. **Given** the theme/display style is changed, **then** every open tab swaps to the new style and the DTO reflects it so that fresh tabs load with that theme.
5. **Given** a new tab/device joins after changes, **then** it inherits the most recent configuration from the backend API.

---

### User Story 3 – Admin UI placement & phased activation

3.1 **Lifecycle controls only** (Priority: P1) – The admin UI must expose only pause/resume/next buttons initially.

3.2 **Full session control** (Priority: P2) – Later enable configuration toggles and back navigation.

**Why this priority**:  
Core control visibility is essential to deliver live commands; full configuration adds value but can come later in the sprint.

**Independent Test**:  
Open PlaylistView as admin and confirm the segmented control is present. Initially only pause/continue/next buttons are active; later enable the four setting inputs and verify they work. Non‑admin tabs never see the control.

**Acceptance Scenarios**:

1. **Given** `_isAdminTab` is true, **when** PlaylistView loads, **then** a segmented control with “Playlist” and “Session Control” tabs is rendered.
2. **Given** the admin selects “Session Control”, **then** the `SessionControls` component is displayed.  In iteration 3.1 only pause/resume/next buttons are enabled; configuration inputs are disabled.  In iteration 3.2 all controls are enabled and the “Back to playlist” button is present.
3. **Given** a non‑admin tab or device loads PlaylistView, **then** the segmented control is omitted entirely.
4. **Given** the gear icon on PlayerView/NextSongView is clicked by an admin, **then** nothing special happens (the optional gear‑modal behaviour has been removed).

---

### User Story 4 – SingerView read‑only playlist mode (Priority: P3)

All users should be able to toggle between library and a read‑only copy of the up‑next list.

**Why this priority**:  
Provides visibility for singers; very low risk and relatively simple.

**Independent Test**:  
On SingerView switch between Library and Playlist; confirm playlist items display but no remove/reorder controls appear.

**Acceptance Scenarios**:

1. **Given** any user on SingerView, **when** they tap the toggle to “Playlist”, **then** they see `UpNextList` with items but no drag handles or remove buttons.
2. **Given** the toggle is set to “Library”, **then** normal search behaviour occurs.

---

### Edge Cases

- What if the admin link is used from a tab that later loses admin privileges? (_Not expected – admin flag is per‑tab and determined on session creation._)
- Numeric input for pause‑between‑songs is negative, non‑integer, or outside the acceptable range (5–90 seconds); invalid values should be clamped or rejected.
- There is only one admin; assume they will not open multiple admin tabs concurrently.
- Two admins issue conflicting configuration updates simultaneously. (_Out of scope given single-admin assumption, but backend should handle last‑write‑wins gracefully if it occurs._)
- Pause/resume events arrive while a tab is mid‑navigation or disconnected.
- Main tab closes while paused – other tabs should continue using their local flag; no backend state.
- Admin attempts controls after session expiry – commands should fail silently or display error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST broadcast pause/resume events to all connected clients via SignalR (`ReceiveSessionPaused`, `ReceiveSessionResumed`).
- **FR-002**: Clients MUST maintain a transient “paused” flag in Fluxor state or module memory; no backend persistence.
- **FR-003**: Admin clients MUST be able to invoke `AdvanceToNextSongAsync(sessionId)` from any page.
- **FR-004**: Session DTO returned by the backend MUST include four runtime options: `singerNameRequired` (bool), `allowReorder` (bool), `pauseBetweenSongs` (int ≥ 0), and `theme` (string).
- **FR-005**: Clients MUST apply configuration changes immediately when received via SignalR or API response.
- **FR-006**: New tabs/devices MUST fetch current configuration from the backend and apply it on load.
- **FR-007**: PlaylistView for admin tabs MUST render a segmented control toggling between playlist and session controls.
- **FR-008**: Non‑admin tabs MUST NOT render the segmented control nor enable admin buttons.
- **FR-009**: SingerView MUST include a library/playlist toggle; playlist mode shows a read‑only `UpNextList`.
- **FR-010**: SessionControls component MUST expose UI for pause/resume/next and the four options plus back‑to‑playlist.
- **FR-011**: Gear icon on PlayerView/NextSongView MAY open SessionControls in a modal when user is admin.
- **FR-012**: Validation for pause‑between‑songs input MUST prevent negative values; non‑numeric entries default to zero or show error.

### Key Entities

- **SessionConfig**: carries runtime options to clients (`SingerNameRequired`, `AllowReorder`, `PauseBetweenSongs`, `Theme`).
- **SessionState**: extended with a `bool IsPaused` flag or separate `SessionControlState` slice.
- **SignalR Events**: `ReceiveSessionPaused`, `ReceiveSessionResumed`, `ReceiveConfigUpdated` (may reuse existing settings broadcast mechanism).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can successfully pause and resume the session from any tab; 100 % of connected tabs stop/continue progression accordingly in manual testing.
- **SC-002**: Configuration toggles propagate to all open tabs within 1‑2 seconds and are adhered to by UI logic.
- **SC-003**: New tabs opened after configuration changes inherit the current settings 100 % of the time.
- **SC-004**: Non‑admin tabs never display any admin controls; 0 % false‑positives in a review of UI.
- **SC-005**: SingerView’s read‑only playlist mode shows correct items and no interactive controls.

## Constitution Review Gates *(mandatory)*

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: All commands and settings flow through SignalR/API; phones/tablets may use them.
- [x] **Backend as source of truth**: Configuration comes from session DTO on backend; non‑main tabs do not rely on sessionStorage/BroadcastChannel.
- [x] **Session ID from backend**: No hard‑coded IDs – admin commands supply the sessionId they already possess.
- [x] **Session parameter validated**: PlaylistView (new UI) and SingerView already validate `?session=`; no new pages introduced except components.

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: Feature deals only with flags and theme strings.
- [x] **Minimal data**: Runtime options are transient and cleared when session expires.
- [x] **Consent-gated telemetry**: Any new Application Insights events would follow existing consent patterns.
- [x] **No sensitive logging**: Logs will reference only session IDs and flag values, no PII.
