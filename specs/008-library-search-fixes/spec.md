# Feature Specification: Library Search UX Fixes

**Feature Branch**: `008-library-search-fixes`  
**Created**: 2026-03-14  
**Status**: Draft  
**Input**: User description: "after our last library search enhancements I noticed the following use cases not working as expected: UC 1: When a user is searching for 'Queen', he gets 3 songs with 'Queen' in their title, like 'Killer Queen', but not the artist 'Queen'. The user should get both titles and artists with 'Queen'. UC 2: the search box should always be visible while I scroll UC 3: the background gradient should always be the same whether 10 songs are shown or 3000 songs are shown. Do not calculate it over the whole height, but keep it fixed. Do not repeat it, but simply behave like it would be a background image that always stays in its place."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Search by Artist Name (Priority: P1)

A singer walks up to the SingerView screen and types "Queen" into the search box. They expect to see all songs by the band Queen, not just songs that happen to have the word "Queen" in their title. Currently the search only matches song titles, so searching for "Queen" shows "Killer Queen" but completely misses every song by the artist Queen (e.g., "Bohemian Rhapsody", "We Will Rock You"). This mismatch between user expectation and behaviour is the most critical fix because it directly breaks the primary purpose of the search feature.

**Why this priority**: This is a functional correctness bug. Users who search for a well-known artist name get incomplete and misleading results. It undermines trust in the search and causes frustration during a live karaoke event.

**Independent Test**: Can be fully tested by searching for an artist name that exactly matches several songs and verifying those songs appear in results alongside any titles that also match.

**Acceptance Scenarios**:

1. **Given** the library contains songs by the artist "Queen", **When** the user types "Queen" in the search box, **Then** both songs with "Queen" in the title (e.g., "Killer Queen") AND songs where "Queen" matches the artist name (e.g., "Bohemian Rhapsody") are shown in the results.
2. **Given** the library is loaded, **When** the user types an artist name that appears in zero song titles, **Then** the results show only artist-name matches from the artist field.
3. **Given** the library is loaded, **When** the user types a partial artist name (e.g., "Quee"), **Then** songs whose artist field starts with or contains "Quee" are included in results.
4. **Given** the library is loaded, **When** the user types a term that matches both a title and an artist for different songs, **Then** both sets of songs are shown without duplicates if the same song matches both fields.

---

### User Story 2 - Sticky Search Box While Scrolling (Priority: P2)

A singer is browsing a long list of search results (e.g., searching for a common word like "love"). They scroll down through hundreds of results. If they want to refine the search, they currently need to scroll all the way back to the top to reach the search input, which is disruptive. The search box should remain visible and accessible at all times, regardless of scroll position.

**Why this priority**: This is a usability issue that becomes increasingly painful the larger the library is. Since Karamel supports libraries of up to 3000+ songs, long result lists are common. Fixing this improves usability without changing any functional behaviour.

**Independent Test**: Can be fully tested by performing a search that returns many results, scrolling to the bottom of the list, and verifying the search box is still visible and interactive at the top of the viewport.

**Acceptance Scenarios**:

1. **Given** a search returns more results than fit on screen, **When** the user scrolls down through the results list, **Then** the search input remains visible and usable at the top of the page without scrolling back up.
2. **Given** the user has scrolled down in the results, **When** the user types in the (now sticky) search box, **Then** the results update as expected and the scroll position resets to show the updated results from the top.
3. **Given** the page is at any scroll position, **When** the user focuses the search input, **Then** the input is reachable and accepts input without any visual obscuring.

---

### User Story 3 - Fixed Background Gradient (Priority: P3)

The SingerView (or library search view) has a decorative background gradient. Currently, the gradient is rendered over the full scrollable height of the page content, so it looks different and washed out when only 3 songs are shown vs. when 3000 songs are shown. The gradient should instead behave like a fixed wallpaper: it always covers the visible viewport, stays in place when the user scrolls, never tiles/repeats, and is visually identical regardless of how many songs are displayed.

**Why this priority**: This is a visual polish issue. It only affects aesthetics, not functionality. It is addressed last because it has no impact on the user's ability to find or add songs.

**Independent Test**: Can be fully tested by comparing the gradient appearance with a result set of 3 songs vs. a result set of 3000 songs and verifying the gradient looks identical in both cases and does not scroll with the content.

**Acceptance Scenarios**:

1. **Given** a search returns only 2–3 results, **When** the user views the page, **Then** the background gradient covers the full visible viewport and looks the same as when hundreds of results are shown.
2. **Given** a large result set that requires scrolling, **When** the user scrolls down, **Then** the background gradient remains visually fixed in place and does not scroll with the content.
3. **Given** any result count, **When** the user views the page, **Then** the gradient does not tile or repeat — the pattern appears exactly once across the viewport.
4. **Given** the browser window is resized, **When** the new viewport size is applied, **Then** the gradient still covers the entire visible area without distortion or gaps.

---

### Edge Cases

- What happens when a search term matches neither a title nor an artist? The result list should be empty with an appropriate "no results" message (existing behaviour, must remain unchanged).
- What happens when an artist name is empty or null for some songs? Those songs should only be matched by title; no errors should occur.
- What happens if the search box becomes sticky but the viewport is very small (e.g., a narrow phone)? The sticky search input must not obscure results to the point of making them unreadable; standard responsive behaviour applies.
- What happens when the user clears the search term after scrolling? The full library list is restored and the scroll position resets to the top of the list.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Search MUST return results that match the query against both the song title AND the artist name fields.
- **FR-002**: A song MUST appear in search results if the query matches either its title or its artist name (or both).
- **FR-003**: Deduplication MUST be applied: if a song matches both title and artist, it MUST appear exactly once in results.
- **FR-004**: The search input MUST remain visible and accessible at the top of the viewport while the user scrolls through results.
- **FR-005**: The background gradient of the library search view MUST be visually fixed relative to the viewport, not the document/content height.
- **FR-006**: The background gradient MUST NOT repeat/tile in any direction.
- **FR-007**: The background gradient MUST appear identical regardless of how many search results are displayed.
- **FR-008**: Scrolling through results MUST NOT cause the gradient to move or change appearance.
- **FR-009**: Existing search behaviour (debouncing, empty-state messages, result counts) MUST be preserved.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user searching for an artist name (e.g., "Queen") sees all songs by that artist in the results, not just songs with the artist name in the title — 100% of artist-matching songs are returned.
- **SC-002**: A user on a page with 500+ results can refine their search without scrolling back to the top — the search input is reachable at any scroll depth.
- **SC-003**: The background gradient is visually indistinguishable between a 3-song result set and a 3000-song result set — verified by side-by-side comparison.
- **SC-004**: The gradient is stable while scrolling — no visible movement of the gradient pattern during scroll at any result count.

## Assumptions

- Karaoke library search is used primarily via the SingerView page, which uses the `LibrarySearch` component.
- Artist and title fields are already present on the `Song` model; no new data fields are needed.
- The sticky search input should use standard CSS positioning (fixed or sticky) without requiring JavaScript-based scroll tracking.
- "Fixed" gradient means the gradient behaves as a viewport-anchored background, not a parallax or animated effect.
- US1 requires a backend query fix in `EfSongRepository` (no new API endpoints or DTO changes). US2 and US3 are frontend CSS only. No data model changes are needed across all three fixes.

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any ❌ must be justified before the spec is approved.
> Full principles: [Karamel-Web Constitution](.specify/memory/constitution.md)

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: All three fixes are purely presentational/filtering changes in the `LibrarySearch` component. They work on phones and tablets opened via QR code without any filesystem or `sessionStorage` dependency.  
- [x] **Backend as source of truth**: No change to data fetching. The search filter runs client-side against data already fetched from the backend.  
- [x] **Session ID from backend**: No change to session handling.  
- [x] **Session parameter validated**: No new pages are introduced.  

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: No change to file path handling.  
- [x] **Minimal data**: No new data is persisted.  
- [x] **Consent-gated telemetry**: No new telemetry events are added.  
- [x] **No sensitive logging**: No new logging is added.
