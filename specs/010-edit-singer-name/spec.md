# Feature Specification: Edit Singer Name in SingerView

**Feature Branch**: `[feature/010-edit-singer-name]`  
**Created**: 2026-03-15  
**Status**: Draft  
**Input**: User description: "REQ-3: Edit Singer Name in SingerView — inline pencil-icon edit with confirm/cancel, empty validation, only when 'Require singer name' session setting is enabled."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Singer Enters Edit Mode and Saves New Name (Priority: P1)

A singer is on the SingerView page. Their name is shown in the header. They want to correct a typo in their name. They tap the name or the pencil icon next to it. The header transforms in-place into a text input pre-filled with their current name and a confirm button. They type the corrected name and tap confirm. The header returns to normal, showing the new name and the pencil icon again.

**Why this priority**: This is the primary use case — the complete rename flow. All other stories depend on this core interaction being present.

**Independent Test**: Can be fully tested by navigating to SingerView with "Require singer name" enabled, tapping the name/pencil, editing, and confirming. Delivers a fully functional rename capability.

**Acceptance Scenarios**:

1. **Given** the singer is on SingerView with "Require singer name" enabled, **When** they tap the singer name in the header, **Then** the header switches to inline edit mode showing a pre-filled text input and a confirm button.
2. **Given** the singer is on SingerView with "Require singer name" enabled, **When** they tap the pencil icon next to the name, **Then** the header switches to inline edit mode.
3. **Given** inline edit mode is active with a non-empty name typed, **When** the singer taps confirm, **Then** the header returns to display state showing the updated name and pencil icon.
4. **Given** a name was saved, **When** the singer subsequently adds songs to the queue, **Then** those queue entries use the updated name.

---

### User Story 2 - Singer Cancels the Edit (Priority: P2)

A singer accidentally taps the pencil icon and does not want to change their name. They tap outside the input field. The header returns immediately to the original name without any change being saved.

**Why this priority**: Required safeguard — without cancel, any accidental tap or mis-type forces an unwanted name change. Follows the "no destructive action without undo" principle.

**Independent Test**: Can be fully tested by entering edit mode, modifying the text, tapping outside the input, and confirming the original name is still shown.

**Acceptance Scenarios**:

1. **Given** inline edit mode is active, **When** the singer taps outside the input field, **Then** edit mode closes and the header shows the original unchanged name.
2. **Given** inline edit mode is active and the singer has partially modified the text, **When** they tap outside, **Then** the modification is discarded.

---

### User Story 3 - Singer Attempts to Save an Empty Name (Priority: P3)

A singer clears the input field and tries to confirm. The change is not saved, the input field shows an error state (e.g., a red border), and edit mode remains active so they can correct the name.

**Why this priority**: Data integrity guard. Allows the other stories to ship first; edit-mode remains usable even if this story is deferred, though the UX would be incomplete.

**Independent Test**: Can be fully tested by entering edit mode, clearing the field, tapping confirm, and verifying the error indicator appears and the name is unchanged.

**Acceptance Scenarios**:

1. **Given** inline edit mode is active with an empty input, **When** the singer taps confirm, **Then** the name is not saved and edit mode remains active.
2. **Given** the previous scenario, **Then** the input field displays a visual error indicator (e.g., red border).
3. **Given** the previous scenario, **When** the singer types a non-empty name and taps confirm, **Then** the name is saved successfully and edit mode closes.

---

### User Story 4 - Edit Controls Hidden When "Require Singer Name" Is Disabled (Priority: P4)

A session is configured without "Require singer name". The SingerView header shows the singer name (or nothing) but does **not** show a pencil icon, and tapping the name does not trigger edit mode.

**Why this priority**: Guards against the feature leaking into sessions where singer identity is not enforced, which would create confusing behavior.

**Independent Test**: Can be fully tested by opening SingerView with the setting disabled and confirming no pencil icon is visible and no edit mode is reachable.

**Acceptance Scenarios**:

1. **Given** "Require singer name" is disabled in session settings, **When** the singer views the SingerView header, **Then** no pencil icon is shown.
2. **Given** "Require singer name" is disabled, **When** the singer taps the name area, **Then** no edit mode is entered.

---

### Edge Cases

- What happens if the singer types only whitespace and confirms? Whitespace-only names must be treated as empty and trigger the same error as a blank field.
- What happens if the singer closes the browser while in edit mode? No partial save should occur; the name remains unchanged.
- What if two rapid taps on the pencil icon are registered? Edit mode should open exactly once (idempotent tap).
- What if the singer's name is very long? The input and header should handle gracefully (truncation or scroll without layout breakage).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pencil icon MUST appear next to the singer name in the SingerView header only when the session setting "Require singer name" is enabled.
- **FR-002**: Tapping either the singer name text or the pencil icon MUST activate inline edit mode in the header.
- **FR-003**: Inline edit mode MUST replace the name display in-place with a text input pre-filled with the current name and a visible confirm button. No modal overlay or page navigation shall occur.
- **FR-004**: Tapping the confirm button with a non-empty, non-whitespace name MUST save the new name and return the header to display mode showing the updated name.
- **FR-005**: Tapping outside the input field MUST cancel the edit and restore the header to display mode with the original unchanged name.
- **FR-006**: Attempting to confirm an empty or whitespace-only name MUST be rejected: the name is not saved, edit mode remains active, and the input field displays a visual error indicator.
- **FR-007**: The feature MUST be entirely absent (no pencil icon, no edit mode) when "Require singer name" is not enabled in the session settings.
- **FR-008**: The updated name MUST be used for all subsequent queue entries added during the same session.

### Key Entities

- **Singer Name**: The display name associated with the current singer on SingerView. Stored client-side for the session. Updated in-place without page reload.
- **Session Setting — Require Singer Name**: A boolean flag in the session configuration that gates visibility of the edit controls.

## Assumptions

- The singer name is already stored in client-side session state (e.g., URL query or Fluxor store) after the singer entered it on the previous step. This feature modifies that stored value.
- "Tapping outside the input field" is interpreted as a focus-loss (blur) event on the input element.
- The confirm button is the only way to commit a change; pressing Enter in the input field is treated as equivalent to tapping confirm (standard form UX).
- The pencil icon is a recognizable edit indicator; an emoji (✏️) or equivalent icon from the existing icon library are both acceptable choices during implementation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A singer can complete a name change (tap pencil → edit → confirm) in under 10 seconds, with no page navigation or modal appearing.
- **SC-002**: 100% of confirm attempts with an empty or whitespace-only field are rejected with a visible error; no empty names are persisted.
- **SC-003**: The pencil icon and edit mode are unreachable in sessions where "Require singer name" is disabled — verified by manual and automated tests.
- **SC-004**: Cancelling an edit (tap outside) always restores the original name without any side effects, including when the field was partially modified.

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any ❌ must be justified before the spec is approved.
> Full principles: [Karamel-Web Constitution](.specify/memory/constitution.md)

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: SingerView is used on phones/tablets via QR code. This feature operates entirely in the client-side singer session state already present on that device — no filesystem or cross-device sessionStorage access is required. ✅
- [x] **Backend as source of truth**: The singer name is session-local UI state, not library or playlist data. The feature does not touch library or playlist queries, so this gate is not applicable. ✅
- [x] **Session ID from backend**: Feature does not create or modify sessions. ✅
- [x] **Session parameter validated**: SingerView already validates the `?session=` parameter. No new pages are added. ✅

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: Feature involves only a singer display name — no file paths. ✅
- [x] **Minimal data**: Only the singer's chosen display name is stored for session use; no additional personal data is collected. ✅
- [x] **Consent-gated telemetry**: No new telemetry events are introduced by this feature. ✅
- [x] **No sensitive logging**: The singer name is a user-chosen display name, not a credential or PII in the legal sense. It MUST NOT be logged beyond debug-level traces (consistent with existing name handling). ✅
