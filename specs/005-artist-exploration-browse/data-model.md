# Data Model: Artist Exploration — Browse Mode

*Phase 1 output — entities, fields, validation rules, and state transitions.*

---

## New Entities

### ArtistItem (Frontend Domain Model)

**File**: `Karamel.Web/Models/ArtistItem.cs`  
**Type**: Immutable record (no persistence, transient UI state)

| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| `Name` | `string` | Artist display name as stored in library | Non-empty (blank artists excluded at API level) |
| `SongCount` | `int` | Number of songs in the session library for this artist | ≥ 1 (groups with 0 songs cannot exist) |

**C#**:
```csharp
/// <summary>Summary entry for the artist browse list.</summary>
public record ArtistItem(string Name, int SongCount);
```

---

## Modified Entities

### LibraryState (Frontend Fluxor State)

**File**: `Karamel.Web/Store/Library/LibraryState.cs`  
**Change**: Three new fields added for artist browse state.

| New Field | Type | Default | Description |
|-----------|------|---------|----|
| `Artists` | `IReadOnlyList<ArtistItem>` | `Array.Empty<ArtistItem>()` | Cached artist list; empty until first load |
| `IsLoadingArtists` | `bool` | `false` | True while `FetchArtistsAsync` is in flight |
| `ArtistsLoaded` | `bool` | `false` | True after first successful load; false after session reset |

**State transitions**:

```
LoadArtistsAction dispatched
  → IsLoadingArtists = true

LoadArtistsSuccessAction dispatched
  → Artists = action.Artists
  → IsLoadingArtists = false
  → ArtistsLoaded = true

LoadArtistsFailureAction dispatched
  → IsLoadingArtists = false
  → ArtistsLoaded = false   (allows retry on next entry to browse mode)
  → ErrorMessage = action.ErrorMessage  (uses existing ErrorMessage field)

ResetPaginationAction dispatched (session change / new scan)
  → Artists = Array.Empty<ArtistItem>()
  → IsLoadingArtists = false
  → ArtistsLoaded = false

ScanProgressAction dispatched with IsComplete == false (library rescan started)
  → Artists = Array.Empty<ArtistItem>()
  → IsLoadingArtists = false
  → ArtistsLoaded = false
```

> **Note**: `ScanProgressAction(IsComplete: false)` is the key invalidation trigger for rescans. Without this, a user who selects a new library folder would see artist names from the previous scan until a full session reset. `ResetPaginationAction` covers session creation/switching; `ScanProgressAction` covers in-session rescans.

---

### ISongRepository (Backend Interface)

**File**: `Karamel.Backend/Repositories/ISongRepository.cs`  
**Change**: One new method added.

```csharp
/// <summary>
/// Returns all distinct artists in the session library,
/// ordered alphabetically, with song counts.
/// Artists with null or whitespace names are excluded.
/// </summary>
Task<IReadOnlyList<ArtistSummaryDto>> GetArtistsAsync(Guid sessionId);
```

---

### EfSongRepository (Backend Implementation)

**File**: `Karamel.Backend/Repositories/EfSongRepository.cs`  
**Change**: Implement `GetArtistsAsync`.

**Query logic**:
```
SELECT Artist, COUNT(*) as SongCount
FROM Songs
WHERE SessionId = @sessionId
  AND Artist IS NOT NULL
  AND Artist != ''
GROUP BY Artist
→ C#-side: .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
```

EF Core translation produces the `GROUP BY` SQL; the final `.OrderBy` with `OrdinalIgnoreCase` is applied in-memory after materialisation to ensure consistent alphabetical order regardless of DB collation (SQLite vs SQL Server).

---

## Backend DTOs

### ArtistSummaryDto

**File**: `Karamel.Backend/Controllers/LibraryDtos.cs` (added to existing file)

```csharp
public record ArtistSummaryDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("songCount")] int SongCount
);
```

---

## Frontend Contracts DTO

### ArtistDto

**File**: `Karamel.Web/Contracts/ArtistDto.cs`  
**Purpose**: Deserialise backend `ArtistSummaryDto` JSON into frontend model.

```csharp
public record ArtistDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("songCount")] int SongCount
);
```

Mapping to domain model: `new ArtistItem(dto.Name, dto.SongCount)`

---

## New Fluxor Actions

**File**: `Karamel.Web/Store/Library/LibraryActions.cs` (added to existing file)

```csharp
// Artist browse actions
public record LoadArtistsAction;
public record LoadArtistsSuccessAction(IReadOnlyList<ArtistItem> Artists);
public record LoadArtistsFailureAction(string ErrorMessage);
```

No `ClearArtistsAction` needed — `ResetPaginationAction` (already dispatched on session reset and new scan) handles clearing.

---

## LibrarySearch Render Logic

The component's render tree gains a new branch:

```
LibrarySearch root
├── Search input box (always shown)
├── Branch A: IsLoading && !Songs.Any() → skeleton loading table (EXISTING)
├── Branch B: ErrorMessage not empty → error alert (EXISTING)
├── Branch C: SearchFilter is empty (NEW — artist browse mode)
│   ├── IsLoadingArtists → spinner
│   └── ArtistsLoaded (or Artists not empty) → artist list rows
│       └── Each row: [artist name] [song count]  [tap → SelectArtist(name)]
├── Branch D: FilteredAndSortedSongs is empty → "no results" alert (EXISTING)
└── Branch E: song results table (EXISTING)
```

**Branch C** replaces the implicit "nothing shown" state when SearchFilter is empty and songs are available. If the user has typed something (SearchFilter non-empty), branches D/E apply as before.

**`SelectArtist(name)`** action:
1. `Dispatcher.Dispatch(new FilterSongsAction(name))`
2. `Dispatcher.Dispatch(new LoadPageAction(Page: 1, SearchQuery: name, Append: false))`

No debounce is needed here (explicit tap action, not incremental typing).
