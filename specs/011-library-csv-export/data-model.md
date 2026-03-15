# Data Model: Library CSV Export

**Branch**: `feature/011-library-csv-export` | **Date**: 2026-03-15

All data for this feature lives entirely in **component-local state** inside `Export.razor`. No new database tables, Fluxor state slices, or sessionStorage keys are introduced.

---

## Component-Local State (`Export.razor`)

| Field | C# Type | Initial Value | Purpose |
|-------|---------|---------------|---------|
| `_songs` | `List<Song>?` | `null` | Song list from the last completed scan. `null` = no scan yet run. |
| `_isScanning` | `bool` | `false` | `true` while `scanDirectory()` awaitable is in progress. |
| `_scanComplete` | `bool` | `false` | `true` after at least one successful scan. Controls download-button visibility. |
| `_scanError` | `string?` | `null` | Non-null if the last scan threw an exception (displayed to user). |
| `_filenamePattern` | `string` | `"%artist - %title"` | Filename pattern passed to `pickLibraryDirectory` via `exportBridge.scanDirectory`. Hardcoded default — no session config available on this page. |

**Derived state** (computed, not stored):
- `ShowDownloadButtons = _scanComplete && !_isScanning`
- `SongCount = _songs?.Count ?? 0` (displayed after scan)

---

## Domain Entities (in-memory only, never persisted)

### Song (existing model — `Karamel.Web/Models/Song.cs`)

Used as-is. Relevant fields for this feature:

| Field | Type | Source | Use in Export |
|-------|------|--------|--------------|
| `Artist` | `string` | Scan result (ID3 / filename pattern) | Artist column in all three CSVs; sort key for `artists.csv`; comparison key for duplicate detection |
| `Title` | `string` | Scan result | Title column in all three CSVs; sort key for `titles.csv`; comparison key for duplicate detection |
| `FullPath` | `string?` | Scan result (relative path from scan root) | FilePath column in `duplicates.csv` |

No new fields are added to `Song.cs`.

---

## In-Memory Computation Structures (`CsvExportHelper.cs`)

These are transient data structures used only during duplicate detection — not stored anywhere.

### ExactDuplicateGroup

```csharp
// Conceptual: matches System.Linq.IGrouping<string, Song>
// Key = Artist.ToLowerInvariant() + "|" + Title.ToLowerInvariant()
// Songs = all Song instances sharing this key
```

**Detection**: `Dictionary<string, List<Song>>` keyed on normalized `Artist|Title`. Populated in one O(n) pass.

### LikelyDuplicateGroup

```csharp
// Conceptual: List<Song> where all members are within (artistThreshold=2, titleThreshold=3)
// of each other (transitively, via Union-Find)
```

**Detection**: O(n²) pair comparison with early-exit → Union-Find clustering → groups of ≥2 songs.

---

## Output Files (browser downloads — no server involvement)

### `artists.csv`

```
Artist;Title
[song rows sorted by Artist ascending, OrdinalIgnoreCase key]
```

- Encoding: UTF-8 (no BOM)
- Delimiter: `;`
- Header: always present
- Sort: `(song.Artist ?? "").ToLowerInvariant()`, `StringComparer.Ordinal`
- Fields with `;`, `"`, newlines: RFC 4180 double-quote escaping

### `titles.csv`

```
Title;Artist
[song rows sorted by Title ascending, OrdinalIgnoreCase key]
```

- Same encoding, delimiter, quoting rules as `artists.csv`
- Sort key: `(song.Title ?? "").ToLowerInvariant()`, `StringComparer.Ordinal`

### `duplicates.csv`

```
Artist;Title;FilePath
[exact duplicate group 1 — all members consecutive]
[exact duplicate group 2 — all members consecutive]
...
[likely duplicate group 1 — all members consecutive]
...
```

- Same encoding, delimiter, quoting rules
- Exact duplicate groups first; likely duplicate groups second
- A song that appears in an exact duplicate group is excluded from likely duplicate comparison
- FilePath = `song.FullPath ?? ""`
- If no duplicates: only header row

---

## Duplicate Detection Algorithm (documented per FR-015)

### Thresholds

| Field | Threshold | Applied After Preprocessing |
|-------|-----------|---------------------------|
| Artist | 2 | Yes |
| Title | 3 | Yes |

### Preprocessing (normalize before comparison)

1. Lowercase: `s.ToLowerInvariant()`
2. Strip leading articles: remove prefix `"the "`, `"a "`, `"an "` (exact string match)
3. Strip punctuation: remove `/`, `-`, `'`, `"`, `,`, `.` characters
4. Collapse whitespace: replace multiple spaces with single space
5. Trim

### Step 1 — Exact Duplicates

```
key(s) = Normalize(s.Artist) + "|" + Normalize(s.Title)
groups = songs.GroupBy(key).Where(g => g.Count() >= 2)
exactSongIds = all Guid ids appearing in any exact group
```

### Step 2 — Likely Duplicates

```
candidates = songs.Where(s => !exactSongIds.Contains(s.Id))
For each unordered pair (a, b) from candidates:
  if |Normalize(a.Artist).Length - Normalize(b.Artist).Length| > 2: continue
  if OSA(Normalize(a.Artist), Normalize(b.Artist)) > 2: continue
  if |Normalize(a.Title).Length - Normalize(b.Title).Length| > 3: continue
  if OSA(Normalize(a.Title), Normalize(b.Title)) > 3: continue
  record pair (a, b) as likely-duplicate candidates

Union-Find: merge all pairs into groups
Output groups with ≥ 2 members
```

### OSA Algorithm

Optimal String Alignment distance (adds transposition to standard Levenshtein, O(m·n) time, O(min(m,n)) space). Implemented as a static method in `CsvExportHelper`. Early-exit: if the minimum possible remaining distance already exceeds the threshold, return threshold+1.

---

## State Transition Diagram

```
         [page load]
              │
              ▼
   ┌─────────────────────┐
   │  _songs=null        │
   │  _isScanning=false  │◄──────────────────────────────┐
   │  _scanComplete=false│                               │ (new folder selected)
   │  [Select Folder btn]│                               │
   └─────────┬───────────┘                               │
             │ click "Select Folder"                     │
             ▼                                           │
   ┌─────────────────────┐                               │
   │  _isScanning=true   │                               │
   │  [Spinner: Scanning…]                               │
   └─────────┬───────────┘                               │
             │ pickLibraryDirectory() returns            │
             ▼                                           │
   ┌─────────────────────┐                               │
   │  _isScanning=false  │                               │
   │  _scanComplete=true │                               │
   │  _songs=[results]   │                               │
   │  [Select Folder btn]│───────────────────────────────┘
   │  [Download Artists] │
   │  [Download Titles]  │
   │  [Download Duplicates]
   └─────────────────────┘
             │
             │ click any Download button
             ▼
   ┌─────────────────────┐
   │  CsvExportHelper    │
   │  generates string   │
   │  → triggerDownload()│
   │  → browser saves    │
   └─────────────────────┘
             (no state change — buttons remain visible)
```
