# Phase 1: Data Model — Song Duration Display

**Date**: 2026-03-01 | **Branch**: `001-song-duration-display`

---

## Entities Changed

### Song (frontend model — `Karamel.Web/Models/Song.cs`)

**Change**: Add `DurationSeconds` property.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DurationSeconds` | `int` | `0` | Duration of the audio/video in whole seconds. Zero means unknown or unavailable. |

**Validation rule**: If `DurationSeconds <= 0`, it is treated as absent — display must omit it entirely (do not show `0:00`).

**Source**: Populated by `fileAccess.js` during library scan (main tab only). Carried in MetadataJson → parsed back by `ConvertJsonToSong` for songs fetched from the backend API.

---

### PlaylistItemDto (frontend contract — `Karamel.Web/Contracts/PlaylistItemDto.cs`)

**Change**: Add `DurationSeconds` property.

| Property | Type | JSON name | Description |
|----------|------|-----------|-------------|
| `DurationSeconds` | `int?` | `durationSeconds` | Duration in seconds for display. Null or zero = unknown; display omits it. |

**Populated by**: Backend `PlaylistHub` extracts it from `song.MetadataJson` when building playlist item responses.

---

## Storage

### MetadataJson — extended schema

`Song.MetadataJson` (`Songs` table, existing column) stores a JSON blob. This feature extends the schema:

**MP3+CDG song (new)**:
```json
{ "durationSeconds": 215 }
```

**Video song (extended)**:
```json
{ "mediaType": "video", "extension": "mp4", "durationSeconds": 180 }
```

**Absent/unknown duration**: MetadataJson is `null` or the `durationSeconds` key is absent. Both cases are treated as zero by all parsers.

**Migration required**: ❌ None. The `MetadataJson` column already exists.

---

## State Transitions

Duration is **read-only after scan**. It does not change when:
- A song is added to or removed from the playlist
- A song's status changes (Queued → UpNext → NowPlaying → Completed)
- The Singer adds a song from a remote device (duration was already captured at scan time)

---

## Display Formatting Rules

| Raw value | Condition | Displayed as |
|-----------|-----------|-------------|
| `0` | Unknown | Hidden (empty) |
| `1` – `3599` | Normal song | `m:ss` (e.g., `3:45`) |
| `3600`+ | Long song | `h:mm:ss` (e.g., `1:02:30`) |

**Shared helper** (implemented once, used in UpNextList and Playlist pages):

```csharp
// Karamel.Web/Helpers/DurationFormatter.cs  (new file)
public static class DurationFormatter
{
    /// <summary>Returns formatted duration string or null if duration is zero.</summary>
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

---

## JavaScript Song Object — extended

`buildDirectorySong`, `buildVideoSong`, and `buildZipSong` in `fileAccess.js` gain a `durationSeconds` field:

```javascript
{
  id: crypto.randomUUID(),
  artist: '...',
  title: '...',
  mp3FileName: '...',
  cdgFileName: '...',
  path: '...',
  fullPath: '...',
  durationSeconds: 215   // NEW — 0 if extraction failed
}
```

This field is serialised into `SongUploadDto.MetadataJson` by `ConvertSongToUploadDto`.

---

## No New Database Migrations

For reference: the Songs table already has `MetadataJson NVARCHAR(MAX) NULL` (SQL Server) / `TEXT NULL` (SQLite). No `dotnet ef migrations add` run is needed for this feature.
