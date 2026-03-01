# Phase 0: Research  Song Duration Display

**Date**: 2026-03-01 | **Branch**: `001-song-duration-display`

---

## Research Question 1: Browser-Side Duration Extraction

### Decision
Use a temporary `<audio>` element's `duration` property, accessed after the `loadedmetadata` event fires, on the File object already held in the main tab via the File System Access API.

### Rationale
- The browser's HTML media element parses only the first few kilobytes of an MP3 (the ID3 header + MPEG frame header) to determine duration. No full file decode is required.
- Works identically for MP4/video via `<video>` element.
- Reliable: `loadedmetadata` event is synchronous to the parser  the `duration` property is always populated at that point (or is `NaN` for corrupt files).
- Does not require a new library: the browser's native API is sufficient.
- `DurationSeconds` is computed as `Math.round(audio.duration)` to return an integer.
- For ZIP-origin songs: the MP3 content is already read as an ArrayBuffer during metadata extraction; create a `Blob` from it and feed to the `<audio>` element.
- For corrupt/malformed files: `audio.duration` returns `NaN` or `Infinity`  coerce to `0`.
- Clean up: revoke the object URL with `URL.revokeObjectURL()` after reading duration.

### Alternatives Considered
| Approach | Why Rejected |
|----------|-------------|
| Web Audio API `AudioContext.decodeAudioData` | Decodes the full file to PCM  ~3050 slower, unnecessary for duration only |
| Server-side media parsing (e.g. FFprobe in container) | Violates privacy principle  file bytes must not leave the main tab |
| jsmediatags TLEN ID3 frame | Optional tag, absent in most karaoke files; unreliable |
| `<audio preload="metadata">` in the DOM | Requires DOM insertion / visible element; temporary in-memory approach is cleaner |

### Implementation Sketch

```javascript
// fileAccess.js  new helper
async function extractDuration(fileOrBlob) {
    return new Promise((resolve) => {
        const url = URL.createObjectURL(fileOrBlob instanceof Blob ? fileOrBlob : new Blob([fileOrBlob]));
        const el = document.createElement('audio');
        const cleanup = () => { URL.revokeObjectURL(url); el.src = ''; };
        el.addEventListener('loadedmetadata', () => {
            const secs = isFinite(el.duration) ? Math.round(el.duration) : 0;
            cleanup();
            resolve(secs);
        }, { once: true });
        el.addEventListener('error', () => { cleanup(); resolve(0); }, { once: true });
        el.preload = 'metadata';
        el.src = url;
    });
}
```

---

## Research Question 2: Duration Storage  MetadataJson vs Dedicated Column

### Decision
Store `durationSeconds` **inside the existing `MetadataJson` blob** (e.g., `{"durationSeconds":215}` for MP3+CDG, or `{"mediaType":"video","extension":"mp4","durationSeconds":180}` for video).

### Rationale
- Zero DB migration cost. `MetadataJson` is already a nullable `nvarchar(max)` on the `Songs` table (SQL Server) and a `TEXT` column (SQLite).
- The `SongUploadDto` comment already says: `// Future: duration, genre, album, year`  this is the intended use.
- Data size impact: `,"durationSeconds":215` adds  20 bytes per song. For 2 000 songs that is < 40 KB total across all rows.
- The existing `ConvertJsonToSong` already parses MetadataJson for `mediaType` and `extension`; extending it for `durationSeconds` is a one-liner.
- `ConvertSongToUploadDto` currently serialises MetadataJson only for video songs. It must be extended to always include `durationSeconds` (even for MP3+CDG songs), using an anonymous object that merges the existing fields.

### Alternatives Considered
| Approach | Why Rejected |
|----------|-------------|
| New `DurationSeconds INT` column + EF migration | Requires running `dotnet ef migrations add` for both SQL Server and SQLite providers; cross-provider migration risk; disproportionate for a single integer |
| Store as separate DTO field outside MetadataJson | Would require a migration anyway to add a backend column; same cost as above |

---

## Research Question 3: PlaylistItemDto Duration Flow for Remote Devices

### Decision
Add `durationSeconds` to the `PlaylistItemDto` (frontend). Populate it in the `PlaylistHub` when building its SignalR responses by parsing the song's `MetadataJson`.

### Rationale
- `PlaylistItemDto` already carries `artist` and `title` from the backend  duration follows the same pattern.
- Remote devices (phones via QR code) rely entirely on `PlaylistItemDto` for queue display. If duration is not in the DTO it cannot be shown remotely.
- The backend `PlaylistHub` already joins song data when constructing playlist item responses (line 929: `i.MetadataJson`). Adding duration extraction there is a three-line change.
- Backend `Song.MetadataJson` is populated on upload; by the time a remote device views the queue, duration is already stored.

### PlaylistHub change sketch (backend C#)

```csharp
// Helper method added to PlaylistHub or a static util class
private static int ParseDuration(string? metadataJson)
{
    if (string.IsNullOrWhiteSpace(metadataJson)) return 0;
    try
    {
        using var doc = JsonDocument.Parse(metadataJson);
        return doc.RootElement.TryGetProperty("durationSeconds", out var p) && p.TryGetInt32(out var d) ? d : 0;
    }
    catch { return 0; }
}
```

---

## Research Question 4: Player View Progress Bar  Polling Approach

### Decision
Use a Blazor `Timer` (1-second interval) that fires only while `showControls == true`. Each tick calls a new JS function `getPlaybackPosition()` in `player.js` that returns `{ currentTime, duration }`. The Blazor component stores `playbackProgress` (0.01.0) and calls `StateHasChanged()`.

### Rationale
- The `audioElement.currentTime` and `videoElement.currentTime` are already available in `player.js`  no new state tracking needed.
- A 1-second polling interval matches SC-006 ("updates at least once per second") without flooding the JS interop channel.
- The timer is started in `ShowControls()` and stopped in `HideControls()`, so there is zero overhead when the overlay is hidden.
- The progress bar uses a simple `<div style="width: @(playbackProgressPercent)%">`  no JS canvas or complex rendering.
- `pointer-events: none` CSS prevents click/drag (FR-009).

### Alternatives Considered
| Approach | Why Rejected |
|----------|-------------|
| JS  .NET callback every `timeupdate` (every ~250 ms) | Too many cross-boundary calls; `timeupdate` fires ~4 per second |
| BroadcastChannel for position | Unnecessary complexity; progress bar is local to the main tab only |
| HTML `<progress>` element | Clickable by default in some browsers; harder to disable seek without JS override |

---

## All Unknowns Resolved

| ID | Unknown | Resolution |
|----|---------|-----------|
| U1 | How to read duration in-browser | Temporary `<audio>` element + `loadedmetadata` event |
| U2 | Where to persist duration | Inside existing `MetadataJson` blob |
| U3 | How remote devices receive duration | Via `durationSeconds` field in `PlaylistItemDto` (SignalR) |
| U4 | How progress bar updates | 1 s timer, calls `player.js getPlaybackPosition()`, updates Blazor state |
