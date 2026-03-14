# Research: Library View Enhancements

**Feature**: 006-library-view-enhancements  
**Date**: 2026-03-10

---

## Finding 1 — Root cause of the gradient shift (US2)

**Decision**: The gradient is already applied to `html` in `tokens.css` (line ~175: `background: var(--gradient-light)`), but without `background-attachment: fixed`. A `linear-gradient(35deg, …)` uses the *farthest-corner algorithm*: the gradient endpoint is computed from the element's box diagonal — `sqrt((w·sin35°)² + (h·cos35°)²)`. When "Load More" appends rows, `html` grows taller, increasing the diagonal and shifting which hue appears at any fixed Y-coordinate. Users see this as the gradient "jumping" at the moment content is appended.

**Rationale**: `background-attachment: fixed` makes the gradient computed against the **viewport** (`100vw × 100vh`) instead of the element's intrinsic size. The gradient is then frozen regardless of how tall `html` grows. This is the standard CSS fix for scroll-stable page backgrounds.

**Alternatives considered**:
- Apply gradient only to `.singer-header` via `background: var(--gradient-light)` — already present on `.singer-header`, but same issue applies if `.singer-header` has dynamic height. More importantly, the background *behind* the scrollable list content area (the `html`/`body` background) is what the user sees shifting as they scroll, so fixing only `.singer-header` is insufficient.
- Use a fixed-position `::before` pseudo-element with the gradient — more complex, same effect as `background-attachment: fixed`, not necessary.
- Move to a solid color background — loses design intent; rejected.

**Known caveat**: `background-attachment: fixed` does not apply within elements that have `transform`, `filter`, or `will-change` applied (they create new stacking contexts). The current `html` element has none of these. Safe to use.

**Also**: `.singer-header` in `SingerView.razor.css` also has `background: var(--gradient-light)`. Since `html` will now be the stable, viewport-anchored gradient, the `.singer-header` should either:
1. Also get `background-attachment: fixed` so it matches the `html` gradient pixel-for-pixel (seamless look), or
2. Be given a distinct surface color to visually distinguish the header card.
Option 1 is preferred for this feature — it matches the existing design intent and requires a single CSS line addition.

---

## Finding 2 — body background interaction

**Decision**: `body` currently has no `background` property set (only `font-family` in `app.css`). Bootstrap 5 sets `body { background-color: var(--bs-body-bg) }`. The token `--bs-body-bg: var(--gradient-light)` is a gradient, and `background-color` ignores gradients per spec. So the `body` effectively has no background — the `html` element's gradient shows through. This means fixing `html` is sufficient; `body` does not need a `background: transparent` override.

**Rationale**: No two-layer gradient issue exists today. The `html` rule is the single source to fix.

---

## Finding 3 — A-Z alphabet bar: layout choice (US1)

**Decision**: **Vertical right strip** (iOS/Android Contacts pattern), not a horizontal bar.

**Rationale**:
- On mobile, the touch-target per letter in a horizontal 26-letter bar would be ~16px wide, below the recommended 44px minimum. A vertical strip gives each letter ~22-26px height with drag support — the user can drag their finger to quickly scan letters, matching the native Contacts UX.
- A horizontal bar placed at the top collides with the browser keyboard when it slides up. A right-side vertical strip sits outside the keyboard's interference zone.
- Users already know this pattern from iOS/Android; no learning curve.

**Alternatives considered**:
- Horizontal bar at the top below search: touch targets too narrow, keyboard collision risk.
- Horizontal bar fixed at bottom: conflicts with bottom browser navigation (Safari safe-area issue).
- No navigation, only section headers with plain scroll: does not satisfy FR-002 (tapping a letter must scroll instantly).

---

## Finding 4 — Scroll mechanism for letter jump in Blazor WASM

**Decision**: Use `id="letter-{X}"` attributes on section header elements, and scroll via `element.scrollIntoView({ behavior: 'instant', block: 'start' })` called from a new `alphabetBridge.js` ES module using JSInterop (`IJSObjectReference`).

**Rationale**:
- `behavior: 'instant'` avoids conflicting with touch momentum scrolling on mobile Chrome/Safari. `behavior: 'smooth'` feels sluggish on Android and can race with touch scroll handlers.
- `block: 'start'` aligns the section header to the top of the viewport — the correct scroll target for A-Z nav.
- `IJSObjectReference` (lazy-load via `import()`) avoids loading the module until the component is rendered, consistent with how `player.js` and other bridges are loaded in this project.
- HTML `href="#letter-A"` anchor links were rejected because they change the URL hash, causing Blazor router re-renders.
- Blazor `<Virtualize>` was rejected because it removes DOM nodes outside the visible range; `getElementById('letter-S')` would return `null` for a virtualised list.

**Alternative for scrollable overflow container** (if `.artist-list` ever gets a `max-height: overflow-y: auto` wrapper): switch to `container.scrollTop = header.offsetTop - container.offsetTop` in the JS module. Currently the list is in the page scroll, so `scrollIntoView` is sufficient.

---

## Finding 5 — Section headers in the artist list

**Decision**: Insert a sticky `<div id="letter-{X}" class="artist-section-header">` before each letter group. The header uses `position: sticky; top: 0` so it "pins" as the user scrolls through a letter's artists.

**Rationale**:
- Satisfies FR-005 ("section header visible at the top of that group after navigation").
- `position: sticky` within the page scroll context works without any JS. The section header of the current letter sticks at the top as the user scrolls through it.
- Since the alphabet strip is a right-side `position: fixed` element (not inside the scroll container), there is no stacking order conflict.

**Alternatives considered**:
- Only show the letter in the alphabet strip (no in-list headers): violates FR-005 and FR-002 acceptance scenario 5 ("section header visible after navigation").

---

## Finding 6 — Artist grouping: computed property vs. Fluxor state

**Decision**: Compute `artistGroups` (grouped-by-letter + `activeLetters` set) as a **private computed property / method in `LibrarySearch.razor` `@code`**, derived from `LibraryState.Value.Artists` on each render. Do **not** add new Fluxor state.

**Rationale**:
- Artist grouping is pure presentational logic — it has no business meaning and no consumer outside the component.
- Constitution Principle III: "business rules belong in domain/service classes, not in Razor components" — grouping for display is not a business rule.
- The artist list is loaded once per browse session and has at most a few hundred entries; recomputing groupings on each render is negligible cost (microseconds).
- Adding a Fluxor action/reducer for grouping would violate YAGNI (constitution Principle VII: "Introduce DDD building blocks only when they reduce complexity or enforce an invariant").

---

## Finding 7 — logger.js integration for alphabetBridge.js

**Decision**: Use `createLogger('AlphabetBridge')` for any debug/warn statements in `alphabetBridge.js`.

**Rationale**: Constitution Principle III requires all new JS modules to use `createLogger` rather than bare `console.log`. The scroll function is too simple to need Info-level logging, but should log a Warning if the target element is not found (element missing = DOM inconsistency worth surfacing).

---

## All NEEDS CLARIFICATION items resolved

| Item | Resolution |
|------|-----------|
| Why does gradient shift? | `html { background: linear-gradient(35deg,...) }` grows with content height. Fix: `background-attachment: fixed`. |
| Where is gradient defined? | `tokens.css` line 54 / 72 / 105 / 137 — `linear-gradient(35deg, #C84A46, #D89A62)`. |
| Is `.singer-header` double-applying the gradient? | Yes, but both on `html` and on `.singer-header`. Both need `background-attachment: fixed`. |
| Best letter-nav layout for mobile? | Vertical right strip (iOS Contacts pattern). |
| scrollIntoView parameters? | `{ behavior: 'instant', block: 'start' }` |
| Use Virtualize? | No — section headers must stay in DOM for scrollIntoView to work. |
| New Fluxor state for grouping? | No — computed in component's `@code` section. |
