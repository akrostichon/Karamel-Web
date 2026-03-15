# Quickstart: Player Controls — Next & Previous Buttons

**Branch**: `009-player-next-prev`
**Feature**: Rename Stop -> Next button; add Previous button to PlayerView

## Manual Verification

### Prerequisites
- Application running: `dotnet run --project Karamel.Web`
- Chrome or Edge browser (File System Access API required)
- A loaded karaoke library with at least one song in the playlist

### Verify REQ-1: Stop renamed to Next

1. Navigate to the PlayerView (a song must be loaded and playing, or UpNext).
2. Hover over the player area to reveal the control overlay.
3. **Expected**: The control row shows three buttons: a skip-back icon (Previous), a play/pause icon, and a **skip-forward icon** (Next). The stop-circle icon is gone.
4. Click the skip-forward (Next) button.
5. **Expected**: Playback stops and the app navigates to the Next Song view — identical behavior to the old Stop button.

### Verify REQ-2: Previous button restarts the song

1. Start playing a song and let it play for 10–15 seconds.
2. Hover to reveal controls; click the skip-back (Previous) button.
3. **Expected**: Playback restarts from 0:00 immediately. The progress bar resets to the beginning.
4. Click Previous multiple times in quick succession.
5. **Expected**: Each click restarts from the beginning. The song never advances to a different queue entry.
6. Pause the song mid-way, then click Previous.
7. **Expected**: The song resets to 0:00 **and begins playing** (not paused).

## Running Tests

### C# tests
```powershell
# From solution root
dotnet test Karamel.Web.Tests
```
Expected: all passing (4 buttons in controls test passes, stop button index test still skipped).

### JavaScript tests
```powershell
cd Karamel.Web\wwwroot
npm run test:run
cd ..\..
```
Expected: `restartPlayback` describe block passes with zero failures.

## Key Files Modified

| File | Change |
|------|--------|
| `Karamel.Web/Pages/PlayerView.razor` | Added Previous button; changed Stop icon to skip-end; added `RestartSong()` method |
| `Karamel.Web/wwwroot/js/player.js` | Added `restartPlayback()` export |
| `Karamel.Web/wwwroot/js/player.test.js` | Added `restartPlayback` describe block |
| `Karamel.Web.Tests/PlayerViewTests.cs` | Updated button count (3->4) and Next button index (1->2) |
