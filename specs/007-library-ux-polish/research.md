# Research: Library UX Polish

**Feature**: `007-library-ux-polish`  
**Date**: 2026-03-14  
**Phase**: 0 — Resolved all implementation unknowns before design

---

## Fix 1 — Loading Spinner on Artist Drill-In

### Root Cause Analysis

In `LibrarySearch.razor`, `SelectArtist(name)` dispatches two actions synchronously:

```csharp
Dispatcher.Dispatch(new FilterSongsAction(name));   // sets SearchFilter = name
Dispatcher.Dispatch(new LoadPageAction(1, null, false, name));  // sets IsLoading = true
```

After `FilterSongsAction`, `SearchFilter = name` and `LibraryState.Value.FilteredSongs` immediately
filters `Songs` in-memory by artist name. Because `Songs` still contains the previous page's data
(keyed to a different artist or a general page load), `FilteredSongs` returns nothing. The component
falls into the `else if (!FilteredAndSortedSongs.Any())` branch and renders the "No songs in library"
or "No songs match" message — before the `LoadPageAction` effect has even started the backend fetch.

The existing branch `@if (IsLoading && !Songs.Any())` (lines ~40-60) shows a skeleton only when
`Songs` is completely empty (initial cold load / library scan). During artist drill-in, `Songs` has
old data so this branch never fires.

### Decision: Add `IsLoadingArtistSongs` flag to `LibraryState`

- **What**: New `bool IsLoadingArtistSongs` property in `LibraryState`.
- **When set to `true`**: A new `SelectArtistAction(string ArtistName)` is dispatched by `SelectArtist()`. Its reducer sets `IsLoadingArtistSongs = true`, `SearchFilter = name`, and `ArtistSongsError = null`.
- **When set to `false`**: The existing `LoadPageSuccessAction` reducer is extended to reset it; and the `ClearFilter` path clears it via a new `ClearArtistFilterAction` or by extending `FilterSongsAction`.
- **Rationale**: Keeps the spinner strictly scoped to the artist drill-in path. Text search (`OnSearchInput`) dispatches `FilterSongsAction` + debounced `LoadPageAction` — it never dispatches `SelectArtistAction`, so `IsLoadingArtistSongs` stays `false` during text search.
- **Alternatives rejected**: Using `IsLoading && SearchFilter != ""` would collapse the artist drill-in and text search cases and violate FR-009 (text search must show previous results, not a spinner).

### Decision: Add `ArtistSongsError` for FR-003b error handling

- **What**: New `string? ArtistSongsError` property in `LibraryState`.
- **Set by**: New `LoadPageFailureAction(string ErrorMessage)` dispatched from `LibraryEffects` when the backend fetch throws.
- **Rendered**: Inline error card with a "Tap to retry" button; component holds `_lastSelectedArtist` to re-dispatch `SelectArtistAction` on retry.
- **Rationale**: The existing `ErrorMessage` property is used for library-wide error display. A dedicated `ArtistSongsError` avoids interfering with the global error branch.

### Component rendering change

```text
BEFORE (else-if chain):
  IsLoading && !Songs.Any()  → skeleton loader
  ErrorMessage != null       → global error
  SearchFilter is empty      → artist browse OR songs list
  !FilteredAndSortedSongs.Any() → empty-state (checks Songs.Any() to pick message)
  else                       → songs table

AFTER:
  IsLoading && !Songs.Any()  → skeleton loader (unchanged)
  ErrorMessage != null       → global error (unchanged)
  SearchFilter is empty      → artist browse (unchanged)
  IsLoadingArtistSongs       → artist drill-in spinner  ← NEW branch (inside else-if)
  ArtistSongsError != null   → error card + retry       ← NEW branch
  !FilteredAndSortedSongs.Any() && !IsLoading → empty-state (TotalCount/filter based)
  else                       → songs table (unchanged)
```

---

## Fix 2 — Scroll Position Restore

### Root Cause Analysis

`ClearFilter()` dispatches `FilterSongsAction(string.Empty)` + `LoadPageAction` +
`TryLoadArtistsIfReady()`, which causes a state change that re-renders the artist list.
There is no scroll offset stored anywhere. After re-render, the browser is at whatever position
it was in—usually near the top because the songs list was shorter than the artist list.

### Decision: Ephemeral component-level scroll offset + `OnAfterRenderAsync`

- **Read position**: `alphabetBridge.js` exports a new `getScrollY()` function
  (`return window.scrollY`). Called from `SelectArtist()` before dispatching state changes.
- **Restore position**: A new `scrollToY(y)` function in `alphabetBridge.js` calls
  `window.scrollTo({ top: y, behavior: 'instant' })`. Called from `OnAfterRenderAsync` on the
  next render cycle after `ClearFilter()` sets `_needsScrollRestore = true`.
- **State held in component**: Two fields: `double _savedScrollY` and `bool _needsScrollRestore`.
  Not in Fluxor — never persisted, invisible to other tabs, cleared on navigation (FR-006).
- **Timing**: `ClearFilter()` sets `_needsScrollRestore = true` and dispatches state changes.
  Blazor re-renders the artist list. `OnAfterRenderAsync` detects `_needsScrollRestore`, calls
  `scrollToY`, then clears both fields. This ensures scrolling happens after the DOM is updated.
- **Alternatives rejected**:
  - `Task.Delay(0)` after dispatch — fragile, race condition on slow renders.
  - Storing offset in Fluxor — unnecessary global state for ephemeral UI behaviour.
  - Using `sessionStorage` — violates FR-006 (must not persist across navigations).

---

## Fix 3 — Accurate Empty State Messages

### Root Cause Analysis

The current empty-state discriminator is:

```csharp
if (LibraryState.Value.Songs.Any())
    "No songs match your search criteria."
else
    "No songs in library."
```

Two bugs:
1. `Songs.Any()` is `false` at the instant `SelectArtist` fires (before songs arrive) → "No songs
   in library" flashes even though the library is not empty.
2. Stale state during text search: `FilteredSongs` may be empty momentarily while `IsLoading =
   true` → "No songs match" flashes even though the search hasn't finished.

### Decision: Base message on `TotalCount` and active filter, guarded by `IsLoading`

```text
Empty state conditions (only reached when IsLoadingArtistSongs = false and ArtistSongsError = null):

  Case A: IsLoading = true              → show nothing (text search in flight, FR-009)
  Case B: SearchFilter is non-empty     → "No songs match your search criteria."  (FR-008)
  Case C: TotalCount = 0 and no filter  → "No songs in library."                  (FR-007)
  (Case D: TotalCount > 0, no filter, IsLoading = false → singer deleted/re-scanned; show nothing)
```

- `TotalCount` comes from the latest `LoadPageSuccessAction.TotalCount` persisted in `LibraryState`.
  It accurately reflects the server-side count regardless of what is currently in `Songs`.
- `SearchFilter` is non-empty → someone tapped an artist or typed a term → "No songs match" is correct.
- `TotalCount == 0` AND `SearchFilter` is empty → genuinely empty library.
- **Alternatives rejected**: Using `Songs.Count == 0` is fragile because `Songs` is cleared and
  repopulated during every page fetch.

---

## Fix 4 — A-Z Marker Synchronization After Letter Jump

### Root Cause Analysis

`ScrollToLetter(char letter)` calls `alphabetBridge.scrollToArtistSection(letter)` which runs
`window.scrollTo({ top: offset, behavior: 'instant' })`. The `IntersectionObserver` observing
the zero-height `<div id="letter-X">` headers uses `rootMargin: '-1px 0px -90% 0px'`. Because the
headers are `height: 0; overflow: hidden`, `getBoundingClientRect().top` is unstable near the
threshold and the `isIntersecting` event may not fire reliably for a programmatic instant scroll.
The result: `_currentLetter` stays on whatever letter was last reported by the observer.

### Decision: Direct `_currentLetter` assignment in `ScrollToLetter`

```csharp
private async Task ScrollToLetter(char letter)
{
    _currentLetter = letter;   // immediately correct — no observer roundtrip needed
    StateHasChanged();
    await _alphabetModule!.InvokeVoidAsync("scrollToArtistSection", letter.ToString());
}
```

- The observer-based `OnLetterVisible` callback continues to handle **manual** scrolling
  (user swipes the list). Only programmatic letter-button taps get the direct assignment.
- **Alternatives rejected**:
  - Non-zero header height — would require CSS rework and DOM restructuring; risky regression.
  - `scroll` event listener in JS — would need throttling/debouncing and a new JS→C# callback, higher complexity for a one-line fix.
  - Setting `behavior: 'smooth'` and waiting — not instant, and flickers during the transition.

---

## Fix 5 — A-Z Bar Full Height

### Root Cause Analysis

Current CSS:

```css
.artist-browse-layout { align-items: flex-start; }

.alphabet-bar {
    position: sticky;
    top: 1rem;
    align-self: flex-start;   /* shrink-wraps to content height */
    gap: 1px;
    /* no height set */
}

.alpha-btn {
    height: 1.6rem;           /* fixed per-button height */
    line-height: 1.6rem;
}
```

`align-self: flex-start` causes the bar to be exactly as tall as its 27 buttons × 1.6rem + gaps ≈
45px. On a 700px screen this leaves ~655px of unused space below the last letter.

### Decision: `height: calc(100vh - 2rem)` + `justify-content: space-evenly` on `.alphabet-bar`

```css
.alphabet-bar {
    position: sticky;
    top: 1rem;
    height: calc(100vh - 2rem);   /* fills viewport height under sticky top */
    align-self: auto;              /* no longer flex-start; parent align-items stays flex-start */
    justify-content: space-evenly; /* distribute 27 buttons across full height */
    /* remove: gap: 1px */
}

.alpha-btn {
    /* remove: height: 1.6rem; line-height: 1.6rem */
    /* width: 1.6rem stays */
    display: flex;
    justify-content: center;
    align-items: center;
}
```

- `sticky` + `top: 1rem` + `height: calc(100vh - 2rem)` means the bar always occupies the full
  visible viewport height minus the sticky offset, regardless of how long the artist list is.
  FR-013 satisfied: bar stretches to fill available vertical height.
- `justify-content: space-evenly` distributes all 27 letters from top to bottom with equal gaps.
  FR-014 satisfied: evenly distributed letters.
- Button dimensions become flexible; `display: flex` + centering keeps the letter text correctly
  centred without `line-height`. Tap targets remain at the letters' rendered positions. FR-015 satisfied.
- On very short landscape screens, letters may be closer together but always within the bar's
  actual rendered bounds (no overflow/clipping because `justify-content: space-evenly` distributes
  within the container, not beyond it).
- **Alternatives rejected**:
  - CSS Grid `grid-template-rows: repeat(27, 1fr)` — works equally well but `flex + space-evenly`
    is fewer lines and matches the existing flex setup.
  - `align-self: stretch` — makes the bar as tall as the artist LIST, which on a small list
    produces a shorter bar than the viewport. `height: calc(100vh - 2rem)` is intention-explicit.
  - `min-height: 100vh` — would cause overflow on very short lists if the parent has `overflow: hidden`.

---

## Cross-Cutting: Text Search Previous-Results Behaviour (FR-009)

**Investigation**: Currently, when text is typed, `FilterSongsAction(text)` immediately changes
`SearchFilter`, which changes `LibraryState.Value.FilteredSongs` (in-memory filter of `Songs`).
If the old `Songs` page has entries matching the typed text, they will show transitionally. If not,
`FilteredAndSortedSongs` is temporarily empty.

**Decision**: Suppress the empty-state message when `IsLoading = true` and
`IsLoadingArtistSongs = false`. This means:
- If there are local matches for the partially-typed text, they show (no change in behaviour).
- If there are no local matches while loading, the view shows a blank content area — not an empty-state
  alert. This is minimal flicker and matches the spec: "the previous result set remains visible
  OR no empty state is shown".
- **No need to defer clearing `Songs`**: The in-memory filter naturally handles transitional display.
  Changing `Songs` clearing in the reducer is not needed.

---

## Files Touched Summary

| File | Changes |
|------|---------|
| `Karamel.Web/Store/Library/LibraryState.cs` | `+IsLoadingArtistSongs`, `+ArtistSongsError` |
| `Karamel.Web/Store/Library/LibraryActions.cs` | `+SelectArtistAction`, `+LoadPageFailureAction` |
| `Karamel.Web/Store/Library/LibraryReducers.cs` | Reducers for new actions; extend `ReduceLoadPageSuccess` |
| `Karamel.Web/Store/Library/LibraryEffects.cs` | Dispatch `LoadPageFailureAction` on API error |
| `Karamel.Web/Components/LibrarySearch.razor` | Rendering changes (spinner, empty state, scroll restore, A-Z sync) |
| `Karamel.Web/Components/LibrarySearch.razor.css` | Alphabet bar full-height layout |
| `Karamel.Web/wwwroot/js/alphabetBridge.js` | `+getScrollY()`, `+scrollToY(y)` |
| `Karamel.Web.Tests/ArtistBrowseTests.cs` | New/updated tests for all five behaviours |
