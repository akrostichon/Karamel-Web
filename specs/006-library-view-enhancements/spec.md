# Feature Specification: Library View Enhancements

**Feature Branch**: `006-library-view-enhancements`  
**Created**: 2026-03-10  
**Status**: Draft  
**Input**: User description: "R2.3 from plan. Also currently the gradient for the library view is changing when I load more songs because it uses the whole view and not the currently visible. It should stay stable when I scroll."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Jump to Artist by Letter (Priority: P1)

A singer is browsing the full artist list and wants to jump straight to artists starting with "S" without having to scroll through hundreds of entries. They tap the letter "S" in an alphabet navigation bar and the list instantly scrolls to the first artist beginning with that letter.

**Why this priority**: Artist browse mode was shipped without letter navigation. On a long list (100+ artists), scrolling is the only discovery mechanism, making the feature tiring on mobile. This is the highest-value missing piece from the artist browse spec (R2.3).

**Independent Test**: Open the artist browse view with a library containing artists across multiple letters. Verify the alphabet bar appears, tapping a letter scrolls to the correct section, and letters with no artists are visually distinct.

**Acceptance Scenarios**:

1. **Given** the artist browse mode is active with artists across multiple letters, **When** the user views the screen, **Then** an alphabet navigation bar is visible showing all 26 letters.
2. **Given** the alphabet bar is visible, **When** the user taps a letter that has matching artists, **Then** the artist list scrolls to the first artist whose name begins with that letter.
3. **Given** the alphabet bar is visible, **When** the user taps a letter that has no matching artists, **Then** nothing happens and the letter appears visually dimmed or disabled.
4. **Given** the artist list is being scrolled, **When** the user passes a section, **Then** the corresponding letter in the alphabet bar is highlighted to reflect the current position.
5. **Given** a letter section exists, **When** the user taps it, **Then** a section header (the letter) is visible at the top of that group of artists.

---

### User Story 2 - Stable Gradient When Scrolling or Loading More (Priority: P2)

A singer is viewing search results in the library. As they scroll down or tap "Load More", the decorative gradient background shifts or jumps, causing a disorienting visual glitch. After this fix, the gradient remains visually stable regardless of how much content is loaded or how far the user has scrolled.

**Why this priority**: This is a visual polish issue rather than a functional gap. It does not block usage but causes a jarring experience each time new content loads. Fixing it improves perceived quality and professionalism.

**Independent Test**: Open the library search with results loaded. Tap "Load More" and observe the background. Then scroll up and down. Verify the gradient does not shift, jump, or change position in either scenario.

**Acceptance Scenarios**:

1. **Given** the library search view is displayed with results, **When** the user scrolls down, **Then** the background gradient does not shift, reposition, or change its appearance.
2. **Given** the library search view has results, **When** the user taps "Load More" and additional songs are appended, **Then** the background gradient remains identical to how it appeared before loading more.
3. **Given** any amount of content in the library view, **When** the view is rendered, **Then** the gradient is anchored to the viewport, not to the total height of the content.

---

### Edge Cases

- What happens when the artist list has no artists starting with a given letter? The letter tap is a no-op and the letter is visually dimmed.
- What happens when all artists start with the same letter? All other letters are dimmed; tapping the one active letter scrolls to the top.
- What happens when the library is empty or not yet loaded? The alphabet bar is not shown in artist browse mode.
- What happens when the full list fits on screen without scrolling? The alphabet bar is still displayed for completeness but scrolling is not needed.
- What happens when the user loads more songs mid-search and then scrolls back up? The gradient should remain stable throughout both actions.
- What happens when an artist's name begins with a non-letter character (e.g., "4 Non Blondes", "!K7")? These artists are grouped separately under a `#` bucket and appear in the list, but there is no alphabet bar shortcut for them; users must scroll manually to reach them.

## Requirements *(mandatory)*

### Functional Requirements

**A-Z Letter Jump Navigation**

- **FR-001**: When artist browse mode is active, the view MUST display an alphabet navigation bar containing all 26 letters (A–Z).
- **FR-002**: Tapping an active letter MUST scroll the artist list to the first artist whose name begins with that letter.
- **FR-003**: Letters for which no artist exists in the current filtered list MUST be visually distinguished (e.g., dimmed, lower opacity) and MUST NOT trigger any scroll action when tapped.
- **FR-004**: The alphabet bar MUST remain fixed/sticky so it is always visible while scrolling through the artist list.
- **FR-005**: The artist list MUST render alphabetical section headers (one header per letter group) so the jump target is visually clear after navigation.
- **FR-006**: The alphabet bar MUST NOT be shown when the library is empty or artist browse mode is not active.

**Stable Gradient**

- **FR-007**: The background gradient in the library view MUST be anchored to the viewport (visible area), not to the total scroll height of the content.
- **FR-008**: Loading additional songs via "Load More" MUST NOT cause any visible shift, jump, or change in the background gradient.
- **FR-009**: Scrolling up or down in the library view MUST NOT cause any visible change in the gradient's position or appearance.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user browsing 200+ artists reaches any target letter's section with a single tap, requiring no manual scrolling.
- **SC-002**: 100% of letters with matching artists are tappable and scroll the list to the correct section.
- **SC-003**: The background gradient appearance is visually identical before and after tapping "Load More" — zero perceptible shift on any screen size.
- **SC-004**: Scrolling through any number of loaded results produces no visual change in the gradient.
- **SC-005**: Section headers in artist browse mode are visible after letter-jump navigation, confirming accurate destination.

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any ❌ must be justified before the spec is approved.
> Full principles: [Karamel-Web Constitution](.specify/memory/constitution.md)

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: Both features are purely UI/CSS changes inside the singer-facing library view. No new filesystem access or sessionStorage dependency is introduced; the feature works identically on a phone opened via QR code.
- [x] **Backend as source of truth**: The artist list data already comes from the backend API (`/api/sessions/{id}/library/artists`). No change to data sourcing is required.
- [x] **Session ID from backend**: No new session IDs are created or assumed. Existing session parameter flows are unchanged.
- [x] **Session parameter validated**: No new pages are introduced. Changes are to the existing `LibrarySearch` component which already operates within a validated session context.

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: These are display-only UI enhancements. No file paths are touched.
- [x] **Minimal data**: No new data is persisted. Letter-scroll state is ephemeral in-memory UI state only.
- [x] **Consent-gated telemetry**: No new telemetry events are introduced.
- [x] **No sensitive logging**: No logging changes; no sensitive data involved.

