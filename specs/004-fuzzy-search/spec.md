# Feature Specification: Smart Search — Fuzzy Matching, Relevance Ranking, and Spelling Suggestions

**Feature Branch**: `004-fuzzy-search`  
**Created**: 2026-03-08  
**Status**: Draft  
**Input**: User description: "Implement fuzzy matching for search terms, prioritized search results with relevance ranking, and search suggestions for alternative spellings"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Singer Finds Song Despite Typos (Priority: P1)

A singer at the karaoke machine types a partial or misspelled query into the search box — for example "Bohemian Rapsody" instead of "Bohemian Rhapsody" or "Macarena" instead of "La Macarena". The search returns the correct song alongside other approximate matches, even though the query does not exactly match the song or artist name in the library.

**Why this priority**: Typo tolerance is the most fundamental improvement and the lowest common denominator — it helps every user immediately. Without it, the other stories (ranking, suggestions) have less value. It can be shipped alone as a complete, useful feature.

**Independent Test**: Can be fully tested by typing a query with a 1–2 character typo into the search box and confirming that the expected song appears in the results.

**Acceptance Scenarios**:

1. **Given** the library contains "Bohemian Rhapsody" by Queen, **When** a singer types "Bohemian Rapsody", **Then** the song appears in the results list
2. **Given** the library contains songs by "Michael Jackson", **When** a singer types "Micheal Jackson" (transposed letters), **Then** songs by Michael Jackson appear in the results
3. **Given** the search query is an exact substring match, **When** the singer searches for "Queen", **Then** exact matches still appear (fuzzy matching does not break existing behaviour)
4. **Given** the search query is very short (1–2 characters), **When** the singer types a short query, **Then** the system falls back to substring matching without applying fuzzy scoring (avoids false positives)

---

### User Story 2 - Most Relevant Songs Appear First (Priority: P2)

A singer searches for "Yesterday" and the library has 20+ songs matching that word. Instead of receiving results in alphabetical order, the singer sees exact title matches at the top, followed by partial title matches, followed by songs where only the artist matches — making it effortless to pick the most likely song without scrolling.

**Why this priority**: Relevance ranking makes the experience significantly more intuitive once fuzzy matching is in place. It directly reduces the number of song picks that need to be scrolled through. Depends on P1 (fuzzy matching) to function with full accuracy.

**Independent Test**: Can be fully tested by searching for a common word that appears in multiple song titles and artist names, and verifying that exact title matches appear above partial matches, which appear above artist-only matches.

**Acceptance Scenarios**:

1. **Given** the library contains "Yesterday" (exact title) and "Yesterday Once More" (partial title) and songs by "Yesterday" (artist), **When** a singer searches "Yesterday", **Then** the exact title match "Yesterday" appears before partial matches, which appear before artist matches
2. **Given** a library page returns 50 results via "Load More", **When** the second page loads, **Then** results on page 2 are also sorted by relevance within that page
3. **Given** a search returns only artist matches (no title match), **When** the singer views results, **Then** songs are grouped by artist relevance and secondarily by title alphabetically within that group

---

### User Story 3 - Singer Gets Spelling Suggestions When No Results Found (Priority: P3)

A singer types a query that returns zero results because the spelling is too different from anything in the library. Instead of seeing a dead end, the singer sees a "Did you mean…?" suggestion with one or more similar-sounding artist or title names from the actual library.

**Why this priority**: This closes the loop for complete misses. It is lower priority because P1 (fuzzy matching) already handles most typos, so P3 only fires for edge cases where the input is too different to match. Depends on fuzzy matching infrastructure from P1.

**Independent Test**: Can be fully tested by typing a query with 3+ character differences from any library entry and verifying that at least one suggestion is displayed in the "Did you mean?" area.

**Acceptance Scenarios**:

1. **Given** the library has no entry containing "Beyonsay", **When** a singer types "Beyonsay", **Then** a "Did you mean: Beyoncé?" suggestion appears below the empty results message
2. **Given** suggestions are shown, **When** the singer taps a suggestion, **Then** the suggestion text is inserted into the search box and a new search is triggered automatically
3. **Given** the library has no remotely similar entry to the query, **When** the singer searches with completely random text, **Then** no suggestions are shown and only the empty-results message is displayed
4. **Given** the search returns at least one result (even a fuzzy match), **When** the result list is shown, **Then** no suggestions are displayed (suggestions only appear when results = 0)

---

### Edge Cases

- What happens when the search term is empty or whitespace? System shows the full library (first page), no fuzzy scoring applied.
- What happens when the library has only 1 song? Fuzzy matching and suggestions should work correctly on a single-entry library.
- What happens when multiple songs tie on relevance score? They should be sorted alphabetically by title as a stable secondary sort.
- What happens when the user clears the search after seeing suggestions? The suggestions disappear and the full library view is restored.
- What happens if the fuzzy threshold produces too many low-quality matches? A maximum result cap or minimum similarity threshold prevents noise (exact threshold is an implementation tuning concern, not a business requirement).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The search system MUST return approximate matches for queries that differ from a song title or artist name by up to 2 characters (including insertions, deletions, and substitutions)
- **FR-002**: Results MUST be ordered by relevance: exact title match first, then partial title match, then artist match only, then fuzzy matches — within each relevance tier, results are ordered alphabetically
- **FR-003**: Fuzzy matching MUST only activate when the query is at least 3 characters long; shorter queries use the existing substring match behavior
- **FR-004**: When a search returns zero results, the system MUST display up to 3 alternative search suggestions derived from similar artist or title names in the library
- **FR-005**: Each displayed suggestion MUST be tappable/clickable and, when activated, MUST populate the search input and trigger a new search with the suggestion text
- **FR-006**: Suggestions MUST NOT appear when results are found (even if only approximate/fuzzy matches); suggestions are exclusively a zero-results fallback
- **FR-007**: Relevance-ranked ordering MUST be preserved across paginated loads ("Load More") — subsequent pages continue the relevance-ordered sequence for the same query
- **FR-008**: The feature MUST work equally for singers using the system from remote devices (phones, tablets via QR code link) with no degradation compared to the host device

### Key Entities

- **SearchQuery**: The text string entered by the user including its length; used to determine whether fuzzy logic is applied
- **RelevanceTier**: A classification per result: `ExactTitle` > `PartialTitle` > `ArtistOnly` > `FuzzyMatch`; determines display order
- **SearchSuggestion**: An alternative search term derived from the library when zero results are found; includes the suggested text and the source field (artist or title) it was derived from

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A singer with a query containing 1–2 character typos finds the intended song in the first page of results in at least 90% of cases across a 3,000-song library
- **SC-002**: When a search has an exact title match in the library, that song appears as the first result 100% of the time
- **SC-003**: Search response time (from user stops typing to results displayed) remains under 800ms for a library of 3,000 songs on a standard mobile network connection
- **SC-004**: When a query returns zero exact or fuzzy results, at least one relevant spelling suggestion is displayed within the same response time (under 800ms)
- **SC-005**: Singers can activate a suggestion and see new results without reloading the page

## Assumptions

- Fuzzy matching is implemented server-side; the search API handles the similarity computation rather than the client
- The similarity algorithm used is edit-distance (Levenshtein), which is a standard, well-understood approach for typo tolerance
- A maximum of 3 spelling suggestions is sufficient to guide a singer to the right result without cluttering the UI
- Suggestions are computed from the same song library as the normal search; no external dictionary or spell-checker service is required
- The minimum query length of 3 characters for fuzzy activation is a sensible default; exact tuning is an implementation concern
- Virtualized list rendering (R4.5) is NOT part of this feature; pagination with "Load More" remains the scroll strategy

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any ❌ must be justified before the spec is approved.
> Full principles: [Karamel-Web Constitution](.specify/memory/constitution.md)

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: Search is entirely backend-driven. A singer on a phone via QR code receives the same fuzzy results as the host device — no filesystem or sessionStorage access required.
- [x] **Backend as source of truth**: All search and suggestion logic runs on the backend; the frontend only sends the query and renders returned results.
- [x] **Session ID from backend**: No new session ID handling is introduced. Existing session parameter on the library endpoint is reused.
- [x] **Session parameter validated**: No new pages are introduced. Existing SingerView page already validates the session parameter.

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: Search results contain only Artist and Title. No file path fields (Mp3FileName, CdgFileName) are included in search responses.
- [x] **Minimal data**: The feature stores no new personal data. Suggestions are computed on-the-fly from the library and are not persisted.
- [x] **Consent-gated telemetry**: No new Application Insights events specific to this feature are required. If search analytics are added later, they must go through the existing consent gate.
- [x] **No sensitive logging**: Query text should not be logged in production logs. Any debug logging of search queries must be at Debug/Trace level only and must not propagate to Application Insights.
