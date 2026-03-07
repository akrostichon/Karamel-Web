# Quickstart: Song Duration Display

**Date**: 2026-03-01 | **Branch**: `001-song-duration-display`

This document gives a developer everything they need to start implementing the feature.

---

## Prerequisites

- On branch `001-song-duration-display` (`git branch` should confirm)
- `dotnet build` passes with zero warnings
- `dotnet test Karamel.Web.Tests` shows ≥ 101 passing tests (baseline)
- `cd Karamel.Web/wwwroot && npm run test:run` shows zero failures

---

## Architecture in One Sentence

> Duration is extracted from each audio/video file in the browser during the library scan, stored in the existing `MetadataJson` JSON field, and displayed right-aligned in `UpNextList.razor` and `Playlist.razor`; the player shows a non-interactive progress bar on hover.

---

## Implementation Order

Work bottom-up (data layer → display layer):

1. **JS: `fileAccess.js` — `extractDuration` helper + wire into `buildXxxSong`**
2. **C#: `Song.cs` — add `DurationSeconds`**
3. **C#: `SongDto.cs` — update `ConvertSongToUploadDto` / `ConvertJsonToSong`**
4. **C#: `PlaylistItemDto.cs` — add `DurationSeconds`**
5. **C#: `PlaylistHub.cs` — add `ParseDuration` + include in projections**
6. **C#: `DurationFormatter.cs` — new static helper**
7. **Razor: `UpNextList.razor` — right-aligned duration on each row**
8. **Razor: `Playlist.razor` — right-aligned duration on each queue row**
9. **JS: `player.js` — add `getPlaybackPosition()`**
10. **Razor: `PlayerView.razor` — progress bar + 1 s poll timer**

---

## Key Code Patterns

### 1. Extracting duration in JS (fileAccess.js)

```javascript
// Private helper — uses temporary audio element
async function extractDuration(fileOrBlob) {
    return new Promise((resolve) => {
        const blob = fileOrBlob instanceof Blob ? fileOrBlob : new Blob([fileOrBlob]);
        const url = URL.createObjectURL(blob);
        const el = document.createElement('audio');
        const cleanup = () => { el.src = ''; URL.revokeObjectURL(url); };
        el.addEventListener('loadedmetadata', () => {
            const secs = Number.isFinite(el.duration) ? Math.round(el.duration) : 0;
            cleanup();
            resolve(secs);
        }, { once: true });
        el.addEventListener('error', () => { cleanup(); resolve(0); }, { once: true });
        el.preload = 'metadata';
        el.src = url;
    });
}

// Usage in buildDirectorySong:
const durationSeconds = await extractDuration(fileObj);
return { ..., durationSeconds };
```

For video, use `document.createElement('video')` instead of `'audio'`.

### 2. DurationFormatter helper (C#)

```csharp
// Karamel.Web/Helpers/DurationFormatter.cs
public static class DurationFormatter
{
    public static string? Format(int seconds)
    {
        if (seconds <= 0) return null;
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }
}
```

### 3. Duration display in UpNextList.razor

```razor
<div class="up-next-song-item">
    <div class="up-next-song-position">#@position</div>
    <div class="up-next-song-details flex-grow-1">
        <div class="up-next-song-title">
            <strong>@song.Artist</strong> – @song.Title
        </div>
        @if (!string.IsNullOrEmpty(song.SingerName))
        {
            <div class="small muted-on-surface">
                <i class="bi bi-person-fill"></i> @song.SingerName
            </div>
        }
    </div>
    @{ var dur = DurationFormatter.Format(song.DurationSeconds); }
    @if (dur != null)
    {
        <div class="up-next-song-duration muted-on-surface small">@dur</div>
    }
</div>
```

Add `.up-next-song-duration { flex-shrink: 0; padding-left: 0.5rem; align-self: center; }` to the scoped CSS.

### 4. Progress bar in PlayerView (Razor markup)

```razor
<!-- Inside controls-overlay, when showControls == true, above the control buttons -->
@if (showControls)
{
    @if (playbackDurationSeconds > 0)
    {
        <div class="playback-progress-bar-container">
            <div class="playback-progress-bar-fill"
                 style="width: @(playbackProgressPercent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))%">
            </div>
        </div>
    }
    <div class="controls">
        <!-- existing buttons -->
    </div>
}
```

CSS (PlayerView.razor.css):
```css
.playback-progress-bar-container {
    width: 100%;
    height: 4px;
    background: rgba(255,255,255,0.25);
    border-radius: 2px;
    margin-bottom: 8px;
    pointer-events: none;    /* non-interactive - FR-009 */
}
.playback-progress-bar-fill {
    height: 100%;
    background: var(--k-primary, #EAAE63);
    border-radius: 2px;
    pointer-events: none;
}
```

### 5. Player View C# fields and timer

```csharp
// New fields
private double playbackProgressPercent = 0;
private int playbackDurationSeconds = 0;
private Timer? _progressTimer;

private void ShowControls()
{
    showControls = true;
    // Start progress polling
    playbackDurationSeconds = PlaylistState.Value.CurrentSong?.DurationSeconds ?? 0;
    if (playbackDurationSeconds > 0)
    {
        _progressTimer?.Dispose();
        _progressTimer = new Timer(async _ => await PollPlaybackProgress(), null, 0, 1000);
    }
}

private void HideControls()
{
    showControls = false;
    _progressTimer?.Dispose();
    _progressTimer = null;
}

private async Task PollPlaybackProgress()
{
    try
    {
        if (playerModule == null || playbackDurationSeconds <= 0) return;
        var pos = await playerModule.InvokeAsync<double>("getPlaybackPosition");
        playbackProgressPercent = Math.Clamp(pos / playbackDurationSeconds * 100, 0, 100);
        await InvokeAsync(StateHasChanged);
    }
    catch { /* JS module may be disposed — ignore */ }
}
```

### 6. player.js — getPlaybackPosition

```javascript
export function getPlaybackPosition() {
    if (playerMode === 'cdg' && audioElement) return audioElement.currentTime;
    if (playerMode === 'video' && videoElement) return videoElement.currentTime;
    return 0;
}
```

---

## Testing Checklist

| Layer | What to test |
|-------|-------------|
| JS (Vitest) | `extractDuration` returns correct seconds for a known-duration audio blob; returns 0 for corrupt input; returns 0 for missing element |
| JS (Vitest) | `getPlaybackPosition` returns `audioElement.currentTime` in CDG mode, `videoElement.currentTime` in video mode |
| C# (xUnit) | `DurationFormatter.Format(0)` returns `null`; `Format(215)` returns `"3:35"`; `Format(3661)` returns `"1:01:01"` |
| C# (xUnit) | `ConvertSongToUploadDto` with `DurationSeconds=175` produces MetadataJson containing `"durationSeconds":175` |
| C# (xUnit) | `ConvertJsonToSong` with MetadataJson `{"durationSeconds":175}` sets `DurationSeconds=175` |
| C# (bUnit) | `UpNextList` renders `"3:35"` for a song with `DurationSeconds=215`; renders no duration for `DurationSeconds=0` |
| C# (bUnit) | Progress bar div is present when `showControls=true` and `DurationSeconds>0`; absent when `DurationSeconds=0` |

---

## Run Tests

```powershell
# C# frontend tests
dotnet test Karamel.Web.Tests

# JS tests
cd Karamel.Web/wwwroot
npm run test:run
cd ../..

# Backend integration tests (request user to run — ~40 s)
# dotnet test Karamel.Backend.Tests -v minimal
```
