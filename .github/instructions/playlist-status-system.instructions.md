---
description: "Playlist item status management - Architecture and patterns for working with playlist state"
applyTo: "**/Playlist*.{cs,razor,js}|**/PlaylistItem*.cs|**/PlaylistHub.cs|**/PlayerView.razor|**/NextSongView.razor"
---

# Playlist Status System

## Overview

The playlist uses explicit status tracking for song lifecycle management. The backend SQL database is the single source of truth, with SignalR broadcasting real-time updates to all connected clients.

## Status Enum

```csharp
public enum SongStatus {
    Queued = 0,      // Added to playlist, awaiting play
    UpNext = 1,      // Auto-promoted by backend (next song to play)
    NowPlaying = 2,  // Actively playing in PlayerView
    Completed = 3    // Finished playing, filtered from views
}
```

**Backend**: [Karamel.Backend/Models/PlaylistItem.cs](../../Karamel.Backend/Models/PlaylistItem.cs)  
**Frontend**: [Karamel.Web/Contracts/PlaylistItemDto.cs](../../Karamel.Web/Contracts/PlaylistItemDto.cs)

## Status Transitions

```
Add Song → Queued (0)
    ↓
Auto-Promotion (backend) → UpNext (1) [when no UpNext exists]
    ↓
Advance Action → NowPlaying (2)
    ↓
Song Ends → Completed (3) + next song → NowPlaying
```

## Auto-Promotion Logic

**CRITICAL**: The backend automatically promotes songs to UpNext status in `PlaylistHub.BroadcastPlaylistUpdate`:

**Location**: [PlaylistHub.cs](../../Karamel.Backend/Hubs/PlaylistHub.cs#L456-L472)

```csharp
// Auto-promote first Queued to UpNext if needed
var hasUpNext = playlist.Items.Any(i => i.Status == SongStatus.UpNext);
var firstQueued = playlist.Items
    .Where(i => i.Status == SongStatus.Queued)
    .OrderBy(i => i.Position)
    .FirstOrDefault();

if (!hasUpNext && firstQueued != null)
{
    firstQueued.Status = SongStatus.UpNext;
    await _playlistRepo.UpdateAsync(playlist);
}
```

**When It Runs**: After every playlist mutation (add, remove, reorder, status change)  
**Why It Exists**: Ensures NextSongView always has a song to display (unless queue is empty)  
**Component Impact**: Components don't need to manually set UpNext - it happens automatically

## SignalR Hub Methods

**File**: [Karamel.Backend/Hubs/PlaylistHub.cs](../../Karamel.Backend/Hubs/PlaylistHub.cs)

### AddItemAsync
- **Behavior**: Creates item with `Status = SongStatus.Queued`
- **Auth**: Requires X-Link-Token header

### SetSongStatusAsync(sessionId, itemId, status)
- **Behavior**: Updates individual item status
- **Auth**: Requires X-Link-Token header
- **Broadcast**: Calls `BroadcastPlaylistUpdate` which includes auto-promotion

### AdvanceToNextSongAsync(sessionId)
- **Behavior**:
  1. Marks current `NowPlaying` → `Completed` (sets `CompletedAt`)
  2. Promotes next `Queued` or `UpNext` → `NowPlaying`
- **Auth**: Requires X-Link-Token header
- **Called By**: PlayerView (song end), NextSongView (start next song)

### CompleteCurrentSongAsync(sessionId)
- **Behavior**: Marks current `NowPlaying` → `Completed` WITHOUT advancing to next
- **Auth**: Requires X-Link-Token header
- **Use Case**: Skip/stop current song

### BroadcastPlaylistUpdate (private)
- **Behavior**:
  1. Auto-promotes first Queued → UpNext (if no UpNext exists)
  2. Filters out `Completed` and `NowPlaying` items
  3. Extracts `CurrentSong` (first `NowPlaying` item or null)
  4. Sends `ReceivePlaylistUpdated` to session group

## Frontend State Management

### PlaylistState
**File**: [Karamel.Web/Store/Playlist/PlaylistState.cs](../../Karamel.Web/Store/Playlist/PlaylistState.cs)

```csharp
public record PlaylistState
{
    public List<PlaylistItemDto> Items { get; init; }     // Queued + UpNext only
    public PlaylistItemDto? CurrentSong { get; init; }     // NowPlaying song
}
```

**CRITICAL**: `Items` contains only `Queued` and `UpNext` songs. `Completed` and `NowPlaying` are filtered by backend.

### Actions
**File**: [Karamel.Web/Store/Playlist/PlaylistActions.cs](../../Karamel.Web/Store/Playlist/PlaylistActions.cs)

- `SetSongStatusAction(ItemId, Status)` - Triggers SignalR call
- `AdvanceToNextSongAction()` - Triggers SignalR call
- `UpdatePlaylistFromBroadcastAction(Items, CurrentSong)` - Updates state from SignalR

### Effects
**File**: [Karamel.Web/Store/Playlist/PlaylistEffects.cs](../../Karamel.Web/Store/Playlist/PlaylistEffects.cs)

- `HandleSetSongStatusAction` - Calls `SessionService.SetSongStatusAsync`
- `HandleAdvanceToNextSongAction` - Calls `SessionService.AdvanceToNextSongAsync`

### Reducers
**File**: [Karamel.Web/Store/Playlist/PlaylistReducers.cs](../../Karamel.Web/Store/Playlist/PlaylistReducers.cs)

**CRITICAL**: Status-related actions are **no-ops**. State only updates from `UpdatePlaylistFromBroadcastAction`.

## Component Patterns

### PlayerView.razor
**File**: [Karamel.Web/Pages/PlayerView.razor](../../Karamel.Web/Pages/PlayerView.razor)

**Current Implementation**:
- ✅ Gets Song via `LibraryState.Value.Songs.FirstOrDefault(s => s.Id.ToString() == currentItem.SongId)` (line 136)
- ✅ Dispatches `AdvanceToNextSongAction` on song end (lines 316, 335)
- ❌ **NOT using PlaylistHelpers.GetSongById** - should be refactored for consistency

**Pattern to Follow**:
```csharp
// Get full Song for playback
var currentItem = PlaylistState.Value.CurrentSong;
var song = PlaylistHelpers.GetSongById(LibraryState.Value, currentItem?.SongId);

// On song end
Dispatcher.Dispatch(new AdvanceToNextSongAction());
```

### Playlist.razor
**File**: [Karamel.Web/Pages/Playlist.razor](../../Karamel.Web/Pages/Playlist.razor)

**Current Implementation**:
- ✅ "Now Playing": `PlaylistState.Value.CurrentSong` (line 50)
- ✅ "Up Next": `Items.Where(i => i.Status == 0 || i.Status == 1)` (line 76)
- ✅ Filters Completed items automatically (handled by backend)

**Pattern to Follow**:
```csharp
// Display current song
var currentSong = PlaylistState.Value.CurrentSong;

// Display queue (Queued + UpNext)
var upNextSongs = PlaylistState.Value.Items
    .Where(i => i.Status == 0 || i.Status == 1)  // Queued or UpNext
    .OrderBy(i => i.Position)
    .ToList();
```

### NextSongView.razor
**File**: [Karamel.Web/Pages/NextSongView.razor](../../Karamel.Web/Pages/NextSongView.razor)

**Current Implementation**:
- ✅ Dispatches `AdvanceToNextSongAction` to start next song (line 500)
- ⚠️ Uses **local `GetSongById` helper** (line 412) instead of shared `PlaylistHelpers` - but needs to get song with metadata and filenames for playback.

**Pattern to Follow**:
```csharp
// Use shared helper (NOT local duplicate)
using Karamel.Web.Helpers;

var song = PlaylistHelpers.GetSongById(LibraryState.Value, nextItem?.SongId);

// Advance to next song
Dispatcher.Dispatch(new AdvanceToNextSongAction());
```

## Song Lookup Pattern

**CRITICAL**: `PlaylistItemDto` is minimal (no file paths). Always look up full `Song` from `LibraryState` for playback.

### Shared Helper
**File**: [Karamel.Web/Helpers/PlaylistHelpers.cs](../../Karamel.Web/Helpers/PlaylistHelpers.cs)

```csharp
using Karamel.Web.Helpers;

var song = PlaylistHelpers.GetSongById(LibraryState.Value, playlistItem?.SongId);
if (song == null) {
    // Handle missing song (deleted from library, etc.)
}
```

**Benefits**:
- Centralized lookup logic
- Handles null/empty song IDs
- Type-safe Song object with file handles for playback

## Database Schema

**Table**: `PlaylistItems`

| Column | Type | Notes |
|--------|------|-------|
| Status | INTEGER | NOT NULL, default 0 (Queued) |
| CompletedAt | DATETIME | NULLABLE, set when Status → Completed |

**Cleanup**: Completed items are purged via CASCADE DELETE when session expires (30 min TTL)

## Common Mistakes to Avoid

### ❌ Don't Manually Set UpNext
**Wrong**:
```csharp
Dispatcher.Dispatch(new SetSongStatusAction(itemId, 1)); // Manual UpNext promotion
```

**Right**:
```csharp
// Don't do anything - backend auto-promotes in BroadcastPlaylistUpdate
```

### ❌ Don't Use Magic Numbers
**Wrong**:
```csharp
if (item.Status == 2) { /* NowPlaying */ }
```

**Right**:
```csharp
using Karamel.Web.Contracts;

if (item.Status == (int)SongStatus.NowPlaying) { /* ... */ }
```

### ❌ Don't Create Local GetSongById Helpers
**Wrong**:
```csharp
// Inside component
private Song? GetSongById(string? songId) { /* duplicate logic */ }
```

**Right**:
```csharp
using Karamel.Web.Helpers;

var song = PlaylistHelpers.GetSongById(LibraryState.Value, songId);
```

### ❌ Don't Filter Items in Components
**Wrong**:
```csharp
var queueSongs = PlaylistState.Value.Items
    .Where(i => i.Status != 3); // Filter Completed
```

**Right**:
```csharp
// Backend already filters Completed - use Items as-is
var queueSongs = PlaylistState.Value.Items
    .Where(i => i.Status == 0 || i.Status == 1); // Queued or UpNext
```

## Testing

**C# Frontend Tests**: [Karamel.Web.Tests/PlaylistPageTests.cs](../../Karamel.Web.Tests/PlaylistPageTests.cs)  
**C# Backend Tests**: [Karamel.Backend.Tests/PlaylistHubTests.cs](../../Karamel.Backend.Tests/PlaylistHubTests.cs)

**Test Coverage**:
- ✅ AddItemAsync creates Queued items
- ✅ AdvanceToNextSongAsync transitions NowPlaying → Completed
- ✅ Auto-promotion Queued → UpNext when no UpNext exists
- ✅ BroadcastPlaylistUpdate filters Completed items
- ✅ CurrentSong extraction from NowPlaying status

## Observability

**Backend Logging** (Application Insights):
- All status transitions logged with `_logger.LogInformation`
- Includes sessionId, itemId, status, artist/title
- Auto-promotion events explicitly logged

**Kusto Query Example**:
```kusto
traces
| where timestamp > ago(30m)
| where message contains "Auto-promoted" or message contains "Advanced item"
| project timestamp, message, customDimensions
| order by timestamp desc
```

## Related Documentation

- [Copilot Instructions](.github/copilot-instructions.md) - Multi-session architecture
- [Logging & Observability](.github/instructions/logging-observability.instructions.md) - Telemetry patterns
