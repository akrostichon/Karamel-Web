# Contracts: Library CSV Export

**Branch**: `feature/011-library-csv-export` | **Date**: 2026-03-15

This feature introduces no new REST endpoints or SignalR messages. The contracts below document the internal JS↔C# interop boundary and the CSV output format that operators consume.

---

## JS↔C# Interop: `exportBridge.js`

### `scanDirectory(filenamePattern: string): Promise<SongDto[]>`

| Aspect | Detail |
|--------|--------|
| Module | `Karamel.Web/wwwroot/js/exportBridge.js` |
| C# caller | `Export.razor` — `_module.InvokeAsync<SongDto[]>("scanDirectory", _filenamePattern)` |
| Return | Array of `SongDto` objects (same shape as `fileAccess.js` scan result) |
| Errors | Throws if user cancels directory picker → C# catches `JSException` and sets `_scanError` |

**Return object shape** (existing `SongDto` record — no changes):

```json
{
  "id": "3fa85f64-...",
  "artist": "Queen",
  "title": "Bohemian Rhapsody",
  "mp3FileName": "Queen - Bohemian Rhapsody.mp3",
  "cdgFileName": "Queen - Bohemian Rhapsody.cdg",
  "videoFileName": null,
  "videoExtension": null,
  "mediaType": "mp3cdg",
  "path": "Rock/Queen",
  "fullPath": "Rock/Queen/Queen - Bohemian Rhapsody",
  "sourceType": "directory",
  "zipFileName": null,
  "zipEntryMp3Path": null,
  "zipEntryCdgPath": null,
  "zipFilePath": null,
  "addedBySinger": null,
  "durationSeconds": 354
}
```

*No new JSON properties introduced. Reuses existing `SongDto` record and `ConvertDtoToSong` converter.*

---

### `triggerDownload(content: string, filename: string): void`

| Aspect | Detail |
|--------|--------|
| Module | `Karamel.Web/wwwroot/js/exportBridge.js` |
| C# caller | `Export.razor` — `_module.InvokeVoidAsync("triggerDownload", csvContent, "artists.csv")` |
| `content` | Full UTF-8 CSV string (semicolon-delimited, including header row and `\n`-terminated lines) |
| `filename` | One of `"artists.csv"`, `"titles.csv"`, `"duplicates.csv"` |
| Side effect | Creates `Blob` → object URL → `<a>` click → `URL.revokeObjectURL` (cleanup) |
| Return | `void` |

**Implementation contract**:
```javascript
// exportBridge.js
export function triggerDownload(content, filename) {
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
}
```

---

## CSV Output Format

### `artists.csv`

| Property | Value |
|----------|-------|
| Encoding | UTF-8 (no BOM) |
| Delimiter | `;` (semicolon) |
| Line ending | `\n` (LF) |
| Header | `Artist;Title` |
| Sort | By Artist ascending — sort key: `Artist.ToLowerInvariant()`, `StringComparer.Ordinal` (digits/specials before A–Z) |
| Quoting | RFC 4180: wrap field in `"..."` if it contains `;`, `"`, `\n`, or `\r`; escape `"` as `""` |

**Example**:
```
Artist;Title
!T.O.O.H.!;Human Flesh
10cc;I'm Not in Love
Abba;Dancing Queen
The Beatles;Hey Jude
ZZ Top;Sharp Dressed Man
```

---

### `titles.csv`

| Property | Value |
|----------|-------|
| Encoding | UTF-8 (no BOM) |
| Delimiter | `;` (semicolon) |
| Line ending | `\n` (LF) |
| Header | `Title;Artist` |
| Sort | By Title ascending — same sort conventions as `artists.csv` |
| Quoting | Same RFC 4180 rules |

**Example**:
```
Title;Artist
Bohemian Rhapsody;Queen
Dancing Queen;Abba
Hey Jude;The Beatles
I'm Not in Love;10cc
```

---

### `duplicates.csv`

| Property | Value |
|----------|-------|
| Encoding | UTF-8 (no BOM) |
| Delimiter | `;` (semicolon) |
| Line ending | `\n` (LF) |
| Header | `Artist;Title;FilePath` |
| Quoting | Same RFC 4180 rules |
| Ordering | Exact duplicate groups first (each group: all members consecutive), then likely duplicate groups |
| Empty | Header row only if no duplicates detected |

**FilePath column**: Value is `song.FullPath ?? ""`. For directory-scanned songs, this is the relative path from the scan root plus the base filename (e.g., `Rock/Queen/Queen - Bohemian Rhapsody`). For ZIP-origin songs, it is the base filename only.

**Example** (2 exact duplicates + 1 likely-duplicate pair):
```
Artist;Title;FilePath
Queen;Bohemian Rhapsody;Rock/Queen/Queen - Bohemian Rhapsody
Queen;Bohemian Rhapsody;Duplicates/Queen - Bohemian Rhapsody
Abba;Dancing Queen;Pop/Abba/Abba - Dancing Queen
Abba;Dancing Queen;Pop/Abba - Dancing Queen
ABBA;Dancing Queen;Pop/ABBA - Dancing Queen
```

*(Lines 2–3: exact duplicate group. Lines 4–6: likely duplicate group — "Abba" vs "ABBA" is within artist threshold 2 after normalization; same title.)*

---

## `CsvExportHelper` — Public Surface

```csharp
// Karamel.Web/Helpers/CsvExportHelper.cs
namespace Karamel.Web.Helpers;

public static class CsvExportHelper
{
    // Thresholds (documented per FR-015)
    public const int ArtistLevenshteinThreshold = 2;
    public const int TitleLevenshteinThreshold  = 3;

    /// Generates artists.csv content. Sorted by Artist ascending (digits/specials first, case-insensitive).
    public static string GenerateArtistsCsv(IEnumerable<Song> songs);

    /// Generates titles.csv content. Sorted by Title ascending (same sort rules).
    public static string GenerateTitlesCsv(IEnumerable<Song> songs);

    /// Generates duplicates.csv content. Exact groups first, then likely groups.
    public static string GenerateDuplicatesCsv(IEnumerable<Song> songs);

    // --- Internal helpers (internal for testing) ---

    /// Normalizes a string for duplicate comparison: lowercase, strip articles, strip punctuation, trim.
    internal static string NormalizeForComparison(string value);

    /// Escapes a CSV field per RFC 4180 (semicolons as delimiters).
    internal static string EscapeCsvField(string value);

    /// OSA (restricted Damerau-Levenshtein) distance with early-exit.
    /// Returns threshold+1 if distance > earlyExitThreshold (avoids unnecessary computation).
    internal static int OsaDistance(string a, string b, int earlyExitThreshold);

    /// Finds groups of exact duplicates (≥2 songs sharing the same normalized Artist|Title).
    internal static IReadOnlyList<IReadOnlyList<Song>> FindExactDuplicateGroups(IEnumerable<Song> songs);

    /// Finds groups of likely duplicates (≥2 songs within Levenshtein threshold, excluding exact duplicates).
    internal static IReadOnlyList<IReadOnlyList<Song>> FindLikelyDuplicateGroups(
        IEnumerable<Song> songs,
        IReadOnlyCollection<Guid> exactDuplicateSongIds);
}
```
