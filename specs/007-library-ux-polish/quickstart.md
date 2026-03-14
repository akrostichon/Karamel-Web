# Quickstart: Library UX Polish

**Feature**: `007-library-ux-polish`  
**Branch**: `007-library-ux-polish`

---

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- Chrome or Edge (File System Access API required for full end-to-end testing)

---

## Running the App

```powershell
# From solution root
dotnet run --project Karamel.Web
# Navigate to http://localhost:5245
# Open a Singer View session and browse artists
```

---

## Manual Testing Checklist

### Fix 1 — Loading Spinner

1. Open Singer View on a device with a slow or throttled connection (Chrome DevTools → Network → "Slow 4G").
2. Browse to the artist list.
3. Tap any artist.
4. **Expected**: A spinner appears immediately. No "No songs in library" or "No songs match" text is ever visible while loading.
5. Once songs load, spinner disappears and song list is shown.

**Error path** (disconnect network, tap artist): Spinner appears, then an inline "Could not load songs. Tap to retry." message appears. Tapping retry re-fetches.

### Fix 2 — Scroll Position Restore

1. Open Singer View with a library of 30+ artists spanning many letters.
2. Scroll the artist list to the "L–N" range.
3. Tap an artist.
4. Tap the ✕ button to clear the filter.
5. **Expected**: Artist list is restored at the same scroll position — "L–N" artists are visible immediately.

### Fix 3 — Empty State Accuracy

1. In a library with songs loaded, type a search query that matches nothing (e.g., "qqqzzz").
2. **Expected**: Only "No songs match your search criteria." is shown. "No songs in library" is never shown.
3. Clear all songs from the library (or reload with an empty directory). Open the library view.
4. **Expected**: "No songs in library." is shown (not "No songs match your search criteria.").

### Fix 4 — A-Z Marker Sync

1. Open the artist browse list with artists across many letters.
2. Tap the "S" button in the alphabet bar.
3. **Expected**: "S" highlights in the bar immediately, without needing to scroll manually.
4. Tap "A" — "A" should highlight and "S" should un-highlight immediately.

### Fix 5 — A-Z Bar Full Height

1. Open the artist browse list on a tall screen or portrait phone (or resize Chrome DevTools to a tall mobile viewport).
2. **Expected**: The A-Z bar runs from near the top to the bottom of the screen, with letters evenly spaced. No empty gap below the last letter.
3. Rotate to landscape — letters should re-distribute to fill the new height.

---

## Running Tests

### C# Tests

```powershell
# Targeted (fastest — run while iterating):
dotnet test Karamel.Web.Tests --filter "FullyQualifiedName~ArtistBrowseTests"

# Full suite (run before committing):
dotnet test Karamel.Web.Tests
# Expect: ≥ 260 passing, 9 skipped
```

### JavaScript Tests

```powershell
cd Karamel.Web\wwwroot
npm run test:run
# Expect: 0 failures
cd ..\..
```

---

## Key Files

| File | What changed |
|------|-------------|
| `Karamel.Web/Store/Library/LibraryState.cs` | `+IsLoadingArtistSongs`, `+ArtistSongsError` |
| `Karamel.Web/Store/Library/LibraryActions.cs` | `+SelectArtistAction`, `+LoadPageFailureAction` |
| `Karamel.Web/Store/Library/LibraryReducers.cs` | New and updated reducers |
| `Karamel.Web/Store/Library/LibraryEffects.cs` | Error dispatch on page fetch failure |
| `Karamel.Web/Components/LibrarySearch.razor` | Spinner, empty state, scroll restore, A-Z sync |
| `Karamel.Web/Components/LibrarySearch.razor.css` | Alphabet bar full-height layout |
| `Karamel.Web/wwwroot/js/alphabetBridge.js` | `+getScrollY()`, `+scrollToY(y)` |
| `Karamel.Web.Tests/ArtistBrowseTests.cs` | New tests for all five behaviours |
