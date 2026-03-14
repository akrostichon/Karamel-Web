# Data Model: Library UX Polish

**Feature**: `007-library-ux-polish`  
**Date**: 2026-03-14  
**Note**: This feature introduces no new database tables or backend models. All changes are to the
Blazor WASM client-side Fluxor state slice.

---

## Modified: `LibraryState` (Fluxor feature state)

**File**: `Karamel.Web/Store/Library/LibraryState.cs`

### New Fields

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `IsLoadingArtistSongs` | `bool` | `false` | `true` while an artist drill-in fetch is in flight. Drives the spinner branch; prevents empty-state flash. Reset by `LoadPageSuccessAction`, `LoadPageFailureAction`, and `FilterSongsAction("")`. |
| `ArtistSongsError` | `string?` | `null` | Set by `LoadPageFailureAction` when the backend fetch for artist songs fails. Rendered as an inline error card with a retry button. Cleared by `SelectArtistAction` (retry path) and `FilterSongsAction("")` (filter clear). |

### Unchanged Fields (referenced by this feature)

| Field | Type | Notes |
|-------|------|-------|
| `IsLoading` | `bool` | Still used to suppress empty-state during text search (FR-009). |
| `TotalCount` | `long` | Used by empty-state logic (FR-007): "No songs in library" only shown when `TotalCount == 0`. |
| `SearchFilter` | `string` | `""` = artist browse mode. Non-empty = text search or artist drill-in. Used by empty-state (FR-008). |

### State Transitions for `IsLoadingArtistSongs`

```
SelectArtistAction dispatched
  └─ IsLoadingArtistSongs = true, SearchFilter = name, ArtistSongsError = null
       │
       ├─ [backend fetch succeeds] LoadPageSuccessAction
       │    └─ IsLoadingArtistSongs = false   (songs rendered)
       │
       ├─ [backend fetch fails]   LoadPageFailureAction
       │    └─ IsLoadingArtistSongs = false, ArtistSongsError = "Could not load songs. Tap to retry."
       │         │
       │         └─ [user taps retry] SelectArtistAction (same artist)
       │              └─ ArtistSongsError = null, IsLoadingArtistSongs = true  (cycle repeats)
       │
       └─ [user clears filter]    FilterSongsAction("")
            └─ IsLoadingArtistSongs = false, ArtistSongsError = null  (back to artist browse)
```

---

## New Actions

**File**: `Karamel.Web/Store/Library/LibraryActions.cs`

### `SelectArtistAction`

```csharp
public record SelectArtistAction(string ArtistName);
```

**Dispatched by**: `SelectArtist(string name)` in `LibrarySearch.razor`.  
**Purpose**: Marks the start of an artist drill-in. Replaces the bare `FilterSongsAction(name)` call
so we can set `IsLoadingArtistSongs = true` atomically with `SearchFilter = name`.

### `LoadPageFailureAction`

```csharp
public record LoadPageFailureAction(string ErrorMessage);
```

**Dispatched by**: `HandleLoadPageAction` effect in `LibraryEffects.cs` when the HTTP call throws.  
**Purpose**: Dismisses the spinner and propagates an error message to the UI.

---

## New / Updated Reducers

**File**: `Karamel.Web/Store/Library/LibraryReducers.cs`

### New: `ReduceSelectArtistAction`

```csharp
[ReducerMethod]
public static LibraryState ReduceSelectArtistAction(LibraryState state, SelectArtistAction action) =>
    state with
    {
        SearchFilter = action.ArtistName,
        IsLoadingArtistSongs = true,
        ArtistSongsError = null
    };
```

### New: `ReduceLoadPageFailureAction`

```csharp
[ReducerMethod]
public static LibraryState ReduceLoadPageFailureAction(LibraryState state, LoadPageFailureAction action) =>
    state with
    {
        IsLoading = false,
        IsLoadingArtistSongs = false,
        ArtistSongsError = action.ErrorMessage
    };
```

### Updated: `ReduceLoadPageSuccess`

Add `IsLoadingArtistSongs = false` and `ArtistSongsError = null` to the existing return:

```csharp
return state with
{
    Songs = songs,
    CurrentPage = action.Page,
    TotalCount = action.TotalCount,
    ServerSearchQuery = action.SearchQuery,
    IsLoading = false,
    IsLoadingArtistSongs = false,   // ← new
    ArtistSongsError = null,        // ← new
    ErrorMessage = null
};
```

### Updated: `ReduceFilterSongsAction`

Clear drill-in state when filter is cleared (user taps X button):

```csharp
[ReducerMethod]
public static LibraryState ReduceFilterSongsAction(LibraryState state, FilterSongsAction action) =>
    state with
    {
        SearchFilter = action.SearchFilter,
        // Clear drill-in error/loading when returning to browse mode
        IsLoadingArtistSongs = string.IsNullOrEmpty(action.SearchFilter)
            ? false
            : state.IsLoadingArtistSongs,
        ArtistSongsError = string.IsNullOrEmpty(action.SearchFilter)
            ? null
            : state.ArtistSongsError
    };
```

---

## Effects Update

**File**: `Karamel.Web/Store/Library/LibraryEffects.cs`

The existing `HandleLoadPageAction` effect should be updated to catch exceptions and dispatch
`LoadPageFailureAction` instead of swallowing errors:

```csharp
// In the catch block:
Dispatcher.Dispatch(new LoadPageFailureAction("Could not load songs. Tap to retry."));
```

---

## Component State (ephemeral, not Fluxor)

**File**: `Karamel.Web/Components/LibrarySearch.razor` — `@code` block

These are local component fields. They are **not** persisted, **not** shared, and reset when the
component is disposed (page navigation).

| Field | Type | Purpose |
|-------|------|---------|
| `_savedScrollY` | `double` | Scroll offset captured when artist is tapped. Restored after `ClearFilter`. |
| `_needsScrollRestore` | `bool` | Flag checked in `OnAfterRenderAsync` to trigger scroll restoration. |
| `_lastSelectedArtist` | `string?` | Stored when `SelectArtist` is called; used to re-dispatch on retry. |

---

## JavaScript Changes

**File**: `Karamel.Web/wwwroot/js/alphabetBridge.js`

Two new exported functions (pure DOM utilities — no logging path needed):

```javascript
export function getScrollY() {
    return window.scrollY;
}

export function scrollToY(y) {
    window.scrollTo({ top: y, behavior: 'instant' });
}
```

---

## No Backend Changes

This feature makes no changes to:
- Any `Karamel.Backend` controller, hub, repository, or model
- Any database migration
- Any DTO crossing the API boundary
- The `SongDto` record or its converters
