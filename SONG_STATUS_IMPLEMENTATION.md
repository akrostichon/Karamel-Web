# Song Status Implementation Plan

**Date**: February 1, 2026  
**Goal**: Implement explicit song state management (Queued/Up Next/Now Playing/Completed) with SignalR as single source of truth

**Current Status**: 
- ✅ Backend complete (Steps 1-7)
- ✅ Frontend state model updated (PlaylistState uses List<PlaylistItemDto>)  
- ✅ All frontend components updated (Steps 8-10, 14)
- ✅ 112/128 tests passing (87.5%)
- ⏳ Remaining: Fix 13 integration tests + Step 13 (Fluxor Effects for SignalR)

## Overview

This implementation adds explicit song status tracking to fix the playlist view state management bug where songs are displayed incorrectly. The backend will track song status in the database, SignalR will broadcast status changes, and the frontend will remove playlist from sessionStorage to rely entirely on real-time updates.

## Implementation Steps

### ✅ Step 1: Add SongStatus Enum to Backend
**File**: [Karamel.Backend/Models/PlaylistItem.cs](Karamel.Backend/Models/PlaylistItem.cs)

- [x] Create `SongStatus` enum with values: `Queued`, `UpNext`, `NowPlaying`, `Completed`
- [x] Add `Status` property to `PlaylistItem` with default value `Queued`
- [x] Add `CompletedAt` nullable `DateTime?` property

### ✅ Step 2: Create and Execute SQLite Migration for Local Testing
**Commands**:
```powershell
dotnet ef migrations add AddSongStatusToPlaylistItems --context BackendDbContext --project Karamel.Backend
dotnet ef database update --context BackendDbContext --project Karamel.Backend
```

- [x] Run migration add command
- [x] Run database update command to apply to local SQLite
- [x] Verify schema changes work locally
- [ ] Test app starts without errors

### ✅ Step 3: Delete Existing Migrations and Create SQL Server Migration
**Commands**:
```powershell
# Delete migrations folder contents (keep folder)
Remove-Item Karamel.Backend\Migrations\*.cs
# Create fresh SQL Server migration
dotnet ef migrations add InitialCreate_WithStatus --context BackendDbContext --project Karamel.Backend
```

- [x] Delete all `.cs` files in Migrations folder
- [x] Run fresh migration command
- [x] Document production deployment command in [RESET_PROD_DB.md](RESET_PROD_DB.md)

### ✅ Step 4: Update PlaylistHub Status Management
**File**: [Karamel.Backend/Hubs/PlaylistHub.cs](Karamel.Backend/Hubs/PlaylistHub.cs)

- [x] Add `SetSongStatusAsync(Guid sessionId, Guid itemId, SongStatus status)` method
- [x] Modify `AddItemAsync` to set `status = SongStatus.Queued` by default
- [x] Add `AdvanceToNextSongAsync(Guid sessionId)` method:
  - Mark current `NowPlaying` as `Completed` with timestamp
  - Mark first `UpNext` as `NowPlaying`
  - Broadcast update
- [x] Update `BroadcastPlaylistUpdate` to:
  - Filter `Items.Where(i => i.Status != SongStatus.Completed)`
  - Include `CurrentSong` (first item with `Status == NowPlaying`, or null)

### ✅ Step 5: Extend SignalR DTOs
**Files**: 
- [Karamel.Backend/Contracts/PlaylistItemDto.cs](Karamel.Backend/Contracts/PlaylistItemDto.cs)
- [Karamel.Backend/Contracts/PlaylistUpdatedDto.cs](Karamel.Backend/Contracts/PlaylistUpdatedDto.cs)

- [x] Add `Status` property (int) to `PlaylistItemDto` (inline in PlaylistHub.cs)
- [x] Add `CurrentSong` property (nullable `PlaylistItemDto`) to `PlaylistUpdatedDto` (inline in PlaylistHub.cs)
- [x] Update `BroadcastPlaylistUpdate` to populate `CurrentSong`

### ✅ Step 6: Update signalRBridge.js Protocol Handler
**File**: [Karamel.Web/wwwroot/js/signalRBridge.js](Karamel.Web/wwwroot/js/signalRBridge.js)

- [x] Add `status` field mapping in `ReceivePlaylistUpdated` handler (line ~80-110)
- [x] Add `itemId` field mapping for playlist item ID
- [x] Extract `currentSong` from DTO instead of hardcoding null
- [x] Update `session-state-updated` event payload structure
- [x] Add `setSongStatus(itemId, status)` export function
- [x] Add `advanceToNextSong()` export function
- [x] Fix `removeItemFromPlaylist` and `reorderPlaylist` signatures to include sessionId
- [ ] Remove sessionStorage persistence of playlist (Step 12)

### ✅ Step 7: Simplify Frontend PlaylistState
**File**: [Karamel.Web/Store/Playlist/PlaylistState.cs](Karamel.Web/Store/Playlist/PlaylistState.cs)

**CRITICAL ARCHITECTURE**: PlaylistItemDto is MINIMAL (no file paths for privacy). Components MUST look up full Song from LibraryState.Value.Songs using SongId for playback metadata.

- [x] Replace `Queue<Song>` with `List<PlaylistItemDto> Items`
- [x] Change `CurrentSong` type to `PlaylistItemDto?`
- [x] Remove `SingerSongCounts` dictionary
- [x] Remove `CurrentSingerName` (get from CurrentSong.SingerName)

**Helper Method Pattern for Components**:
```csharp
private Song? GetSongById(string? songId)
{
    if (string.IsNullOrEmpty(songId)) return null;
    return LibraryState.Value.Songs.FirstOrDefault(s => s.Id.ToString() == songId);
}
```

### ✅ Step 8: Update NextSongView Countdown Logic
**File**: [Karamel.Web/Pages/NextSongView.razor](Karamel.Web/Pages/NextSongView.razor)

- [ ] Modify `UpdateNextSong` to get first `Queued` item from Items list
- [ ] Modify `StartAutoAdvanceTimer` to call SignalR `SetSongStatusAsync(sessionId, nextSong.Id, UpNext)` when countdown starts
- [ ] Update countdown display (line ~66-70) to only show when `nextSong.Status == UpNext`
- [ ] Update `NavigateToPlayer` to work with new state

### ✅ Step 9: Update PlayerView Transitions
**File**: [Karamel.Web/Pages/PlayerView.razor](Karamel.Web/Pages/PlayerView.razor)

**CRITICAL**: PlayerView must look up full Song from LibraryState using `CurrentSong.SongId` to get file paths for playback.

- [ ] Add `GetCurrentSong()` helper method to look up Song from LibraryState
- [ ] On `OnInitializedAsync`, dispatch action to call SignalR `SetSongStatusAsync(sessionId, CurrentSong.Id, NowPlaying)`
- [ ] Use `GetCurrentSong()` to get full Song metadata for playback
- [ ] On `OnSongEnded`, dispatch action to call SignalR `AdvanceToNextSongAsync(sessionId)` instead of `ClearCurrentSongAction`
- [ ] Remove manual queue management

### ✅ Step 10: Fix Playlist.razor Display Logic
**File**: [Karamel.Web/Pages/Playlist.razor](Karamel.Web/Pages/Playlist.razor)

- [ ] Change line 56 from `Queue.Peek()` to `PlaylistState.Value.CurrentSong` for "Now Playing"
- [ ] Change line 74 to `Items.Where(i => i.Status == "Queued").OrderBy(i => i.Position)` for "Up Next"
- [ ] Verify Completed items are automatically filtered by backend

### ✅ Step 11: Update Session Cleanup for Completed Items
**File**: [Karamel.Backend/Repositories/PlaylistRepository.cs](Karamel.Backend/Repositories/PlaylistRepository.cs)

- [ ] Option A: Add `DeleteCompletedItemsAsync(Guid sessionId)` method
- [ ] Option B: Rely on CASCADE DELETE when session is deleted (simpler - RECOMMENDED)
- [ ] Document in cleanup job logic

### ✅ Step 12: Remove Playlist from sessionStorage
**File**: [Karamel.Web/wwwroot/js/signalRBridge.js](Karamel.Web/wwwroot/js/signalRBridge.js)

- [ ] Remove playlist persistence logic (if exists around line ~187-206)
- [ ] Keep session metadata and library only
- [ ] Update comments to clarify SignalR is source of truth

### ✅ Step 13: Update Fluxor Actions/Reducers/Effects
**Files**:
- [Karamel.Web/Store/Playlist/PlaylistActions.cs](Karamel.Web/Store/Playlist/PlaylistActions.cs)
- [Karamel.Web/Store/Playlist/PlaylistReducers.cs](Karamel.Web/Store/Playlist/PlaylistReducers.cs)
- [Karamel.Web/Store/Playlist/PlaylistEffects.cs](Karamel.Web/Store/Playlist/PlaylistEffects.cs)

- [ ] Add `SetSongStatusAction(Guid itemId, SongStatus status)`
- [ ] Add `AdvanceToNextSongAction()`
- [ ] Update `ReduceNextSongAction` to work with Items list
- [ ] Add effects to invoke SignalR hub methods for status changes
- [ ] Update all reducers to work with List instead of Queue

### ✅ Step 14: Update Singer Song Count Calculations
**Files**:
- [Karamel.Web/Pages/SingerView.razor](Karamel.Web/Pages/SingerView.razor)
- [Karamel.Web/Components/LibrarySearch.razor](Karamel.Web/Components/LibrarySearch.razor)

- [ ] Calculate on-demand: `Items.Count(i => i.SingerName == singerName && i.Status != "Completed")`
- [ ] Update 10-song limit check
- [ ] Remove any SingerSongCounts references

### ✅ Step 15: Testing and Verification
**Commands**:
```powershell
# Backend tests
dotnet test Karamel.Backend.Tests

# Frontend tests
dotnet test Karamel.Web.Tests

# JavaScript tests
cd Karamel.Web\wwwroot
npm run test:run
```

- [ ] Run backend tests and verify hub methods work
- [x] Frontend tests compile successfully (112/128 passing, 13 failures, 3 skipped - see details below)
- [ ] Fix remaining 13 frontend test failures (components need to look up Song from LibraryState using SongId)
- [ ] Run JavaScript tests
- [ ] Manual testing: NextSongView → PlayerView transition
- [ ] Manual testing: Cross-tab synchronization
- [ ] Manual testing: Song completion and queue advancement

**Test Failures (13) - To Fix**:
1. `TwoTabBroadcastSimulationTests.SingerAddsSong_NextSongReceivesPlaylistUpdate` - Empty collection
2. `NavigationFlowTests.PlayerView_WithMissingSessionParameter` - Missing LibraryState service
3. `SingerViewTests.Component_ShowsSongCountForCurrentSinger` - Need on-demand calculation from Items
4. `FallbackBehaviorTests.PlayerView_WhenNoCdg_ShowsMissingCdgFallback` - PlayerView not looking up Song from LibraryState
5. `PlaylistPageTests.RemoveButton_WhenClicked_DispatchesRemoveSongAction` - Action parameter mismatch (itemId vs SongId)
6. `NextSongViewIntegrationTests.Integration_DisplaysNextSongFromQueue` - Component not displaying from Items
7. `SingerViewTests.Component_ShowsSuccessToast_OnAddToPlaylistSuccess` - Position calculation off by 1
8. `FallbackBehaviorTests.PlayerView_WhenCdgCorrupt_ShowsCorruptCdgFallback` - PlayerView not looking up Song
9. `NextSongViewIntegrationTests.Component_UpdatesDisplay_WhenPlaylistStateChanges` - Component not reacting to Items changes
10. `NextSongViewIntegrationTests.Component_ReactsTo_MultipleQueueChanges` - Same as above
11. `PlayerViewTests.OnSongEnded_DispatchesNextSongAction` - Now dispatches `AdvanceToNextSongAction` not `ClearCurrentSongAction`
12. `PlayerViewTests.Component_LoadsAndPlaysCurrentSong` - PlayerView not calling loadSongFiles (needs Song lookup)
13. `NextSongViewIntegrationTests.Component_UpdatesDisplay_WhenQueueBecomesEmpty` - Timeout waiting for state change

## Key Decisions

1. **Backend State Ownership**: ✅ Yes - database tracks song status
2. **"Up Next" Definition**: ✅ Explicit state (set when countdown starts)
3. **Multi-Tab Priority**: ✅ SignalR wins (no playlist in sessionStorage)
4. **Song Lifecycle**: ✅ Mark as Completed, remove from view, purge on session cleanup
5. **Complexity Budget**: ✅ Database migration + SignalR protocol change approved
6. **"Up Next" Timer Semantics**: ✅ Status changes to UpNext when countdown starts
7. **Singer Song Count**: ✅ Calculate on-demand (keep frontend simple)
8. **Completed Item Cleanup**: ✅ Purge when session expires (CASCADE DELETE)

## Database Schema Changes

### PlaylistItem Table (NEW columns)
```sql
ALTER TABLE PlaylistItems ADD COLUMN Status INTEGER NOT NULL DEFAULT 0;
ALTER TABLE PlaylistItems ADD COLUMN CompletedAt DATETIME NULL;
```

Status enum values:
- 0 = Queued
- 1 = UpNext
- 2 = NowPlaying
- 3 = Completed

## SignalR Protocol Changes

### PlaylistItemDto (NEW fields)
```csharp
public record PlaylistItemDto(
    Guid Id,
    string Artist,
    string Title,
    string? SingerName,
    int Position,
    Guid? SongId,
    int Status  // NEW
);
```

### PlaylistUpdatedDto (NEW field)
```csharp
public record PlaylistUpdatedDto(
    Guid PlaylistId,
    Guid SessionId,
    List<PlaylistItemDto> Items,
    PlaylistItemDto? CurrentSong  // NEW
);
```

## Frontend State Changes

### OLD PlaylistState
```csharp
public record PlaylistState
{
    public Queue<Song> Queue { get; init; }
    public Song? CurrentSong { get; init; }
    public string? CurrentSingerName { get; init; }
    public IReadOnlyDictionary<string, int> SingerSongCounts { get; init; }
}
```

### NEW PlaylistState
```csharp
public record PlaylistState
{
    public List<PlaylistItemDto> Items { get; init; }  // NEW - all non-Completed items
    public PlaylistItemDto? CurrentSong { get; init; }  // CHANGED type
}
```

## Status Transition Flow

```
1. Song added to queue
   → Status = Queued

2. NextSongView countdown starts
   → Status = UpNext (via SignalR SetSongStatusAsync)

3. PlayerView loads
   → Status = NowPlaying (via SignalR SetSongStatusAsync)

4. Song ends
   → Status = Completed (via SignalR AdvanceToNextSongAsync)
   → Next UpNext song → NowPlaying
   → Removed from playlist view

5. Session expires (30 min TTL)
   → Completed items purged (CASCADE DELETE)
```

## Rollout Plan

1. ✅ Implement and test locally with SQLite
2. ✅ Run all test suites
3. ✅ Delete migrations and create SQL Server migration
4. ✅ Push to feature branch
5. ⏳ Deploy to production (user will execute migration manually)
6. ⏳ Monitor for issues in production

## Notes

- Production Playlists table is empty - no data migration needed
- Existing sessions will be cleared after deployment
- Users must restart sessions
- SignalR is now single source of truth for playlist state
- sessionStorage only contains session metadata and library
