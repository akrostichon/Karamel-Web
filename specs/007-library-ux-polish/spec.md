# Feature Specification: Library UX Polish

**Feature Branch**: `007-library-ux-polish`  
**Created**: 2026-03-14  
**Status**: Draft  
**Input**: User description: "Library UX polish: scroll restore, loading spinner, empty state fix, A-Z sync"

## Overview

This spec captures four targeted UX polish items for the library view introduced in spec 006. Each issue is a self-contained interaction defect that disrupts the browse experience in artist mode.

1. **Scroll restore** — clearing the artist filter should return the artist list to the exact scroll position the user was at before drilling in.
2. **Loading spinner** — clicking an artist should show a loading indicator while the backend fetches songs, preventing confusing empty-state flashes.
3. **Empty state accuracy** — "No songs in library" must only appear when there truly are no songs; a failed or empty search result should say something different.
4. **A-Z marker sync** — after using the scroll buttons (A-Z jump), the highlighted letter in the alphabet bar must track the visible list position correctly.
5. **A-Z bar full height** — the alphabet bar letters are currently clustered near the top of the screen instead of being evenly distributed across the full available vertical space.

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Loading Spinner on Artist Drill-In (Priority: P1)

A singer taps an artist name in the artist browse list. On slower connections or a large library, there is a short delay before the backend returns the song list. During this delay, the interface briefly flashes "No songs match your search criteria" before the songs appear. This is confusing and looks like a bug. After this fix, a loading spinner replaces the blank state while the fetch is in progress, and the song list appears only when data is ready.

**Why this priority**: This is the most visually disruptive issue. It looks like an error rather than a loading state, and it occurs on every artist drill-in on any non-instant connection. Fixing it immediately restores user trust that the app is working correctly.

**Independent Test**: Open artist browse in a environment where loading is not instant (slow network, large library, or artificial delay). Tap any artist. Verify a spinner appears while loading, and the song list appears once the data arrives — never an empty-state message during the transition.

**Acceptance Scenarios**:

1. **Given** the artist browse list is shown, **When** the user taps an artist, **Then** a loading spinner is displayed immediately in place of the song list.
2. **Given** the loading spinner is shown after tapping an artist, **When** the backend returns songs, **Then** the spinner disappears and the song list renders.
3. **Given** the loading spinner is shown, **When** data is loading, **Then** neither "No songs in library" nor "No songs match your search criteria" is displayed.
4. **Given** the backend returns zero songs for an artist (edge case: artist with no songs indexed), **When** the fetch completes, **Then** the spinner disappears and an appropriate empty state is shown (see FR-007/FR-008 for differentiation rules).
5. **Given** the backend fetch fails (network error, timeout, or 5xx), **When** the error is received, **Then** the spinner disappears and an inline error message with a retry action is shown (e.g., "Could not load songs. Tap to retry.").

---

### User Story 2 - Scroll Position Restored on Filter Clear (Priority: P2)

A singer is browsing a long artist list, scrolls halfway down to "M" artists, and taps "Madonna". After reviewing her songs, they tap the clear/back button to return to the artist list. The list jumps back to the top, forcing them to scroll all the way back to "M" to continue browsing. After this fix, clearing the artist filter restores the exact scroll position where the user was when they tapped the artist.

**Why this priority**: This directly interrupts the browsing flow for anyone using the artist view with more than a screenful of artists. Users lose their place in a potentially long list every time they explore any artist. The fix greatly reduces navigation friction.

**Independent Test**: Open artist browse with a library containing 30+ artists spanning multiple letters. Scroll down so that artists in the "L-N" range are visible. Tap one artist, then clear the filter using the X button. Verify the artist list is restored at exactly the same scroll position — the same artists are visible as before drilling in.

**Acceptance Scenarios**:

1. **Given** the artist list is scrolled to a position below the top, **When** the user taps an artist name, **Then** the current scroll offset of the artist list is remembered.
2. **Given** the artist filter is active (a specific artist's songs are shown), **When** the user clears the filter via the X button, **Then** the artist list is restored at the exact scroll offset from when the artist was tapped.
3. **Given** the user navigates away from the library view entirely and returns, **When** artist browse is re-entered, **Then** the scroll position is reset to the top (no persistent memory across sessions or page navigations).
4. **Given** the artist list at the remembered scroll position no longer contains the same items (e.g., library was reloaded), **When** the filter is cleared, **Then** the view scrolls to the top gracefully rather than throwing an error.

---

### User Story 3 - Accurate Empty State Messages (Priority: P3)

A singer searches for "Zeppelin" in the library. The search debounce fires, the backend is queried, and — because the library has no matching songs — the result is empty. The view briefly shows "No songs match your search criteria" and then switches to "No songs in library". This makes it look like the library is empty rather than the search just not matching. After this fix, "No songs in library" only appears when there are genuinely zero songs in the entire library, and "No songs match your search criteria" is shown when a search or filter returns empty results.

**Why this priority**: This is a confusing but non-blocking issue. Users can still browse and search correctly; the message just temporarily misleads them. Fixing it improves trust and clarity.

**Independent Test**: In a library with songs loaded, type a search term that matches nothing. Verify that only "No songs match your search criteria" is ever displayed — never "No songs in library". Then clear the library (or test with an empty library) and verify that "No songs in library" appears correctly.

**Acceptance Scenarios**:

1. **Given** the library contains songs and the user enters a search term that matches nothing, **When** the search completes, **Then** "No songs match your search criteria" is displayed (never "No songs in library").
2. **Given** the library contains songs and an artist filter is active but has no results, **When** the view renders the empty result, **Then** "No songs match your search criteria" is displayed.
3. **Given** the library has no songs at all (empty library), **When** the library view is shown, **Then** "No songs in library" is displayed.
4. **Given** the library contains songs and a text search is in progress, **When** the search fetch is pending, **Then** the previous result set remains visible — no spinner and no empty-state message is shown.
5. **Given** the user clears a search term that had no results, **When** the full library reloads, **Then** the song list appears without an empty-state flash.

---

### User Story 4 - A-Z Marker Stays in Sync After Letter Jump (Priority: P4)

A singer uses the A-Z alphabet bar in artist browse to jump to the letter "R". The list scrolls to the "R" section. However, the highlighted letter in the alphabet bar does not update — it may still show whichever letter was highlighted before, or none at all. As the user then scrolls manually, the highlight resyncs. After this fix, the highlighted letter updates immediately and correctly whenever the view scrolls, whether from manual scrolling or a letter-button tap.

**Why this priority**: This is a cosmetic synchronization defect. The feature still works correctly (the jump lands in the right place); the highlight is just visually stale. It is less disruptive than the other issues but visible on every programmatic scroll.

**Independent Test**: Open artist browse with artists across multiple letters. Use the A-Z buttons to jump to several different letters in sequence. After each jump, verify the tapped letter is highlighted in the alphabet bar immediately — without needing to manually scroll.

**Acceptance Scenarios**:

1. **Given** the alphabet bar is visible and the user taps a letter button, **When** the list scrolls to that letter's section, **Then** the tapped letter is highlighted in the alphabet bar.
2. **Given** the user has used a letter button to jump to a section, **When** the user manually scrolls up or down, **Then** the highlighted letter continues to update based on the currently visible section.
3. **Given** the user taps the same letter button twice in a row, **When** the second tap fires, **Then** the highlight remains on that letter and no incorrect state is shown.
4. **Given** the user jumps to letter "R" and then taps "A", **When** the list scrolls to the top, **Then** "A" is highlighted and "R" is no longer highlighted.

---

### User Story 5 - A-Z Bar Fills Full Vertical Height (Priority: P4)

A singer opens the artist browse view on their phone. The A-Z alphabet bar on the right side of the screen shows all 26 letters bunched together near the top, leaving a large blank gap below. The letters are small and close together rather than spread evenly from the top to the bottom of the screen. After this fix, the alphabet bar stretches to fill the full vertical height of the list container, with the letters evenly distributed from top to bottom so the bar is easy to tap on any screen size.

**Why this priority**: This is a layout defect that reduces the tap target size of each letter button and makes the bar look unfinished. On phones where the view is tall, the unused space below the letters is particularly noticeable. The fix is purely presentational with no data or logic impact.

**Independent Test**: Open artist browse on a tall screen or phone. Verify the A-Z bar extends from the top of the list area to the bottom, with all 26 letters visually evenly spaced. Compare to the attached screenshot showing the incorrect clustered state.

**Acceptance Scenarios**:

1. **Given** the artist browse view is open, **When** the view renders, **Then** the alphabet bar spans the full vertical height of the list container (not just the height of 27 tightly packed letters).
2. **Given** the alphabet bar spans full height, **When** the view renders, **Then** all 26 letters are evenly distributed from top to bottom of the bar.
3. **Given** the alphabet bar is full height, **When** the screen is resized (e.g., rotating the device), **Then** the bar adapts to fill the new available height and letters remain evenly spaced.
4. **Given** the bar is full height, **When** the user taps a letter, **Then** the tap target is accurately mapped to the correct letter — no offset caused by the layout change.

---

### Edge Cases

- What if the user taps an artist during a very fast connection and the spinner appears for less than 100ms? The spinner is still shown — no minimum display time required; it simply disappears when data arrives.
- What if the scroll position was at the very top when the artist was tapped? Clearing the filter scrolls to the top (same behavior), which is correct and indistinguishable from the previous behavior.
- What if the list is so short that it does not scroll (all artists fit on screen)? The remembered scroll offset is zero and restoring it has no visible effect, which is fine.
- What if the user taps a letter in the alphabet bar that corresponds to a section entirely off-screen in a large list? The list scrolls to that section and the letter is highlighted immediately.
- What if the library is loading when the user taps an artist? The action is either queued until load completes, or the artist tap is disabled while loading — no double-spinner states.
- What if a search term is cleared while a previous search fetch is still in-flight? The in-flight request is cancelled or its result is ignored; no stale empty-state message is shown from the abandoned request.
- What if the backend returns an error when loading an artist's songs? The spinner is dismissed and an inline error message with a retry action is shown; the user is never left in a blank or misleading state.
- What if the artist list has fewer than 26 entries and the bar is shorter than the screen? The bar still fills the full available vertical height and letters remain evenly spaced regardless of list length.
- What if the browser window is very short (e.g., landscape on a small phone)? The letters may be very close together but must still be distributed across the full height — no clipping or overflow that truncates letters.

## Requirements *(mandatory)*

### Functional Requirements

**Loading Indicator**

- **FR-001**: When the user taps an artist in artist browse mode, the view MUST display a loading indicator immediately while the backend fetch is pending.
- **FR-002**: The loading indicator MUST disappear and be replaced by the song list as soon as the backend responds with data.
- **FR-003**: While the loading indicator is shown, neither the "No songs in library" nor the "No songs match your search criteria" empty-state messages MUST be displayed.
- **FR-003b**: If the backend fetch fails (network error, timeout, or 5xx), the loading indicator MUST be dismissed and the view MUST display an inline error message with a retry action (e.g., "Could not load songs. Tap to retry.").

**Scroll Position Restore**

- **FR-004**: The artist browse view MUST record the current scroll offset when the user taps an artist name to drill into it.
- **FR-005**: When the artist filter is cleared (X button), the artist list MUST restore to the exact scroll offset recorded at the time the artist was tapped.
- **FR-006**: The remembered scroll offset MUST NOT persist across page navigations or library reloads; on those events the scroll position MUST reset to the top.

**Empty State Accuracy**

- **FR-007**: "No songs in library" MUST only be displayed when the paginated API response returns `totalCount == 0` AND no search term or artist filter is currently active.
- **FR-008**: When a search term or artist filter is active and returns zero results, the view MUST display "No songs match your search criteria" instead of "No songs in library".
- **FR-009**: While a **text search** fetch is in progress, the view MUST keep the previous result set visible and MUST NOT show a spinner or any empty-state message. The loading spinner (FR-001) is exclusive to the artist drill-in action.
- **FR-010**: If a pending search request is superseded by a newer request, the result of the older request MUST be discarded and MUST NOT trigger any empty-state or content update.

**A-Z Marker Synchronization**

- **FR-011**: After a letter button in the alphabet bar is tapped and the list scrolls to that section, the tapped letter MUST be highlighted in the alphabet bar immediately upon scroll completion.
- **FR-012**: The alphabet bar highlighted letter MUST always reflect the letter of the first visible section in the artist list, whether the scroll was triggered by a button tap or by the user manually scrolling.

**A-Z Bar Layout**

- **FR-013**: The alphabet bar container MUST stretch to fill the full available vertical height of the list area, not shrink-wrap to the natural height of its letter items.
- **FR-014**: The 27 letter items MUST be evenly distributed (equal spacing) from the top to the bottom of the bar container.
- **FR-015**: The letter tap targets MUST remain accurately mapped to their visible positions after the layout change — tapping a letter MUST trigger navigation to the correct section.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: When tapping an artist on any connection speed, users see a loading indicator — the empty-state messages ("No songs in library" / "No songs match your search criteria") are never visible while data is loading.
- **SC-002**: After drilling into an artist and returning to the artist list, 100% of users land at the same scroll position they left — the list does not jump to the top.
- **SC-003**: "No songs in library" is never shown when the library contains songs, regardless of the active search term or filter.
- **SC-004**: After tapping any letter in the A-Z bar, the correct letter is highlighted in the alphabet bar immediately — no manual scroll is needed to trigger the update.
- **SC-005**: The A-Z bar visually spans the full height of the list area on all screen sizes — no blank gap below the last letter.
- **SC-006**: All five polish behaviors work correctly when the library view is opened on a secondary device (phone/tablet) via QR code, without relying on local state.

## Clarifications

### Session 2026-03-14

- Q: When an artist is tapped and the backend fetch fails (network error, 5xx, timeout), what should happen after the spinner is shown? → A: Dismiss the spinner and show an inline error message (e.g., "Could not load songs. Tap to retry.") with a retry action.
- Q: How does the frontend determine the library is truly empty vs. a search returning zero results? → A: Use the existing `totalCount` field from the paginated API response; show "No songs in library" only when `totalCount == 0` and no search term or artist filter is active.
- Q: During a text search (debounce pending or fetch in-flight), should the view show a spinner or keep previous results visible? → A: Keep the previous result set visible — no spinner for text search. The spinner is reserved for the explicit artist drill-in action only.

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any ❌ must be justified before the spec is approved.
> Full principles: [Karamel-Web Constitution](.specify/memory/constitution.md)

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: Yes. All five changes are pure UI/UX corrections (scroll memory, spinner visibility, message logic, letter highlight, bar layout). None require filesystem access or sessionStorage. They work equally on a phone opened via QR code.
- [x] **Backend as source of truth**: Yes. The loading/empty-state logic is driven by the backend API response status. Scroll offset is ephemeral in-memory UI state (not persisted to sessionStorage).
- [x] **Session ID from backend**: Not applicable — no new session identifiers are introduced.
- [x] **Session parameter validated**: Not applicable — no new pages are added.

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: Not applicable — no file path changes.
- [x] **Minimal data**: Yes. The only new state is an ephemeral in-memory scroll offset (a number), which is never persisted or transmitted.
- [x] **Consent-gated telemetry**: Not applicable — no new telemetry events.
- [x] **No sensitive logging**: Not applicable — no new logging introduced.
