# Quickstart: Implementing Library View Enhancements

**Feature**: 006-library-view-enhancements  
**Branch**: `006-library-view-enhancements`  
**Date**: 2026-03-10

---

## Prerequisites

```powershell
git checkout 006-library-view-enhancements
dotnet build          # must be zero errors, zero warnings
dotnet test Karamel.Web.Tests   # baseline: ≥260 passing, 9 skipped
cd Karamel.Web\wwwroot
npm run test:run      # baseline: zero failures
cd ..\..
```

---

## US2 First: Stable Gradient (simpler, no new JS module)

**Why first**: Zero risk change; single CSS line addition. Validates clean baseline before adding JS.

### Step 1 — Add `background-attachment: fixed` to `tokens.css`

File: `Karamel.Web/wwwroot/css/tokens.css`

Find the existing `html` block (near the bottom of the file):

```css
html {
  color-scheme: light dark;
  background: var(--gradient-light);
  color: var(--color-text);
  font-family: var(--font-sans);
}
```

Add `background-attachment: fixed;` and `background-size: cover;`:

```css
html {
  color-scheme: light dark;
  background: var(--gradient-light);
  background-attachment: fixed;
  background-size: cover;
  color: var(--color-text);
  font-family: var(--font-sans);
}
```

### Step 2 — Match `background-attachment: fixed` on `.singer-header`

File: `Karamel.Web/Pages/SingerView.razor.css`

Find the `.singer-header` block:

```css
.singer-header {
    ...
    background: var(--gradient-light);
    ...
}
```

Add `background-attachment: fixed;` so the header's gradient is computed against the same viewport anchor as `html`, producing a seamless blend:

```css
.singer-header {
    ...
    background: var(--gradient-light);
    background-attachment: fixed;
    ...
}
```

### Step 3 — Visual test

Run the app (`dotnet run --project Karamel.Web`), open SingerView, scan a library with ≥50 songs. Tap "Load More" repeatedly. Verify the gradient does not shift. Scroll up and down. Verify gradient is frozen.

---

## US1: A-Z Letter Jump Navigation

### Step 4 — Create `alphabetBridge.js`

File: `Karamel.Web/wwwroot/js/alphabetBridge.js`

```javascript
import { createLogger } from './logger.js';

const logger = createLogger('AlphabetBridge');

/**
 * Scroll the artist list to the section header for the given letter.
 * Uses 'instant' behavior — avoids conflict with touch momentum scrolling on mobile.
 * @param {string} letter - Single uppercase letter (e.g. "S")
 */
export function scrollToArtistSection(letter) {
    const el = document.getElementById(`letter-${letter}`);
    if (el) {
        el.scrollIntoView({ behavior: 'instant', block: 'start' });
    } else {
        logger.warn(`scrollToArtistSection: element #letter-${letter} not found`);
    }
}
```

### Step 5 — Create `alphabetBridge.test.js`

File: `Karamel.Web/wwwroot/js/alphabetBridge.test.js`

Write Vitest tests covering:
- `scrollToArtistSection('S')` on a DOM with `#letter-S` present → calls `scrollIntoView`
- `scrollToArtistSection('Z')` on a DOM without `#letter-Z` → emits a warning, does not throw

### Step 6 — Update `LibrarySearch.razor` markup (Branch C — artist browse mode)

Group artists by first letter in `@code`:

```csharp
private record ArtistGroup(char Letter, IReadOnlyList<ArtistItem> Artists);
private IReadOnlyList<ArtistGroup> _artistGroups = [];
private HashSet<char> _activeLetters = [];
private IJSObjectReference? _alphabetModule;

private void BuildArtistGroups()
{
    _artistGroups = LibraryState.Value.Artists
        .GroupBy(a => char.ToUpperInvariant(a.Name.Length > 0 ? a.Name[0] : '#'))
        .OrderBy(g => g.Key)
        .Select(g => new ArtistGroup(g.Key, g.ToList()))
        .ToList();
    _activeLetters = _artistGroups.Select(g => g.Letter).ToHashSet();
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        _alphabetModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/alphabetBridge.js");
    }
}

private async Task ScrollToLetter(char letter)
{
    if (_alphabetModule is not null)
        await _alphabetModule.InvokeVoidAsync("scrollToArtistSection", letter.ToString());
}
```

Call `BuildArtistGroups()` inside the state-change handler that triggers when `ArtistsLoaded` becomes `true`.

Replace the artist list markup in Branch C with grouped rendering:

```razor
<div class="artist-browse">
    <div class="artist-list">
        @foreach (var group in _artistGroups)
        {
            <div id="letter-@group.Letter" class="artist-section-header" aria-label="Artists starting with @group.Letter">
                @group.Letter
            </div>
            @foreach (var artist in group.Artists)
            {
                <div class="artist-row" @onclick="() => SelectArtist(artist.Name)">
                    <span class="artist-name">@artist.Name</span>
                    <span class="artist-song-count">@artist.SongCount @(artist.SongCount == 1 ? "song" : "songs")</span>
                </div>
            }
        }
    </div>

    <nav class="alphabet-bar" aria-label="Jump to letter">
        @foreach (var letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            var isActive = _activeLetters.Contains(letter);
            <button
                class="alpha-btn @(isActive ? "active" : "inactive")"
                disabled="@(!isActive)"
                @onclick="@(isActive ? () => ScrollToLetter(letter) : (Func<Task>?)null)"
                aria-label="Jump to artists starting with @letter">
                @letter
            </button>
        }
    </nav>
</div>
```

**Dispose** the module reference in `Dispose()`:

```csharp
public void Dispose()
{
    // ... existing disposal ...
    _alphabetModule?.DisposeAsync();
}
```

### Step 7 — Add CSS to `LibrarySearch.razor.css`

```css
/* A-Z alphabet navigation strip */
.library-search .artist-browse {
    position: relative;
    padding-right: 36px;
}

.library-search .artist-section-header {
    position: sticky;
    top: 0;
    z-index: 2;
    background: var(--color-surface-alt, var(--color-surface));
    padding: 0.25rem 1rem;
    font-size: 0.75rem;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--color-text-secondary);
    border-bottom: 1px solid var(--color-border, rgba(0,0,0,0.08));
}

.library-search .alphabet-bar {
    position: fixed;
    right: 0;
    top: 50%;
    transform: translateY(-50%);
    z-index: 20;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1px;
    padding: 0.25rem 2px;
    background: rgba(var(--color-surface-contrast-rgb), 0.6);
    backdrop-filter: blur(8px);
    border-radius: 12px 0 0 12px;
    touch-action: none;
}

.library-search .alpha-btn {
    all: unset;
    width: 28px;
    height: 22px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.65rem;
    font-weight: 700;
    border-radius: 4px;
    cursor: pointer;
    line-height: 1;
    touch-action: manipulation;
}

.library-search .alpha-btn.active {
    color: var(--color-text);
}

.library-search .alpha-btn.inactive,
.library-search .alpha-btn:disabled {
    color: rgba(var(--color-text-rgb), 0.25);
    cursor: default;
    pointer-events: none;
}

.library-search .alpha-btn.active:hover {
    background: rgba(var(--color-text-rgb), 0.08);
}
```

### Step 8 — Extend `ArtistBrowseTests.cs`

Add bUnit tests for:
- 26 letter buttons rendered when artists are loaded; letters with no artists have `disabled` attribute
- Tapping an active letter calls the JSInterop function with the correct letter string
- Section headers are rendered (one per unique first letter)
- Alphabet bar is hidden when artist browse mode is not active (library not scanned)

### Step 9 — Run tests

```powershell
# C# tests (targeted first)
dotnet test Karamel.Web.Tests --filter "FullyQualifiedName~ArtistBrowseTests"

# JS tests (targeted first)
cd Karamel.Web\wwwroot
npx vitest run js/alphabetBridge.test.js
cd ..\..

# Full suite
dotnet test Karamel.Web.Tests
cd Karamel.Web\wwwroot
npm run test:run
cd ..\..
```

---

## Checkpoint: Acceptance Scenarios

| Scenario | Verified by |
|----------|-------------|
| Alphabet bar visible with artists loaded | `ArtistBrowseTests` |
| 26 letters shown; missing letters dimmed | `ArtistBrowseTests` |
| Tapping letter triggers scrollIntoView | `ArtistBrowseTests` + `alphabetBridge.test.js` |
| Section headers present between groups | `ArtistBrowseTests` |
| Alphabet bar hidden when not in browse mode | `ArtistBrowseTests` |
| Gradient stable on Load More | Manual visual test (no feasible DOM snapshot test) |
| Gradient stable on scroll | Manual visual test |
