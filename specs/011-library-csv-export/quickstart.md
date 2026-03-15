# Quickstart: Library CSV Export

**Branch**: `feature/011-library-csv-export` | **Date**: 2026-03-15

Implementation quick-reference for the developer working on this feature. See [research.md](research.md), [data-model.md](data-model.md), and [contracts/export-contracts.md](contracts/export-contracts.md) for full rationale.

---

## Overview

Three new files + one new Razor page + one test file:

| New File | Purpose |
|----------|---------|
| `Karamel.Web/Pages/Export.razor` | Standalone `/export` page — no session |
| `Karamel.Web/Pages/Export.razor.css` | Scoped styles |
| `Karamel.Web/Helpers/CsvExportHelper.cs` | All CSV logic: sort, escape, Levenshtein, duplicate detection |
| `Karamel.Web/wwwroot/js/exportBridge.js` | JS interop: delegate scan to `fileAccess.js`, trigger download |
| `Karamel.Web.Tests/ExportPageTests.cs` | bUnit component tests for `Export.razor` |
| `Karamel.Web/wwwroot/js/exportBridge.test.js` | Vitest unit tests for `exportBridge.js` |

No backend changes. No new NuGet packages. No Fluxor state additions.

---

## Step 1 — `exportBridge.js`

Create `Karamel.Web/wwwroot/js/exportBridge.js`:

```javascript
import { createLogger } from './logger.js';
import { pickLibraryDirectory } from './fileAccess.js';

const logger = createLogger('ExportBridge');

/**
 * Scan a directory using the File System Access API.
 * @param {string} filenamePattern - e.g. '%artist - %title'
 * @returns {Promise<Array>} Array of song DTOs
 */
export async function scanDirectory(filenamePattern) {
    logger.info('Starting directory scan for export');
    const songs = await pickLibraryDirectory(filenamePattern);
    logger.info('Scan complete', { count: songs.length });
    return songs;
}

/**
 * Trigger a browser download for a CSV string.
 * @param {string} content - Full UTF-8 CSV content
 * @param {string} filename - e.g. 'artists.csv'
 */
export function triggerDownload(content, filename) {
    logger.info('Triggering CSV download', { filename });
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

## Step 2 — `CsvExportHelper.cs`

Key implementation guidance:

### Sorting

```csharp
// Sort by Artist: normalize key, then Ordinal comparison
var sorted = songs.OrderBy(s => (s.Artist ?? "").ToLowerInvariant(), StringComparer.Ordinal);
```

### CSV field escaping (RFC 4180, semicolon delimiter)

```csharp
internal static string EscapeCsvField(string value)
{
    if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        return $"\"{value.Replace("\"", "\"\"")}\"";
    return value;
}
```

### Normalization (for duplicate comparison only — NOT for CSV output)

```csharp
internal static string NormalizeForComparison(string value)
{
    var v = value.ToLowerInvariant().Trim();
    foreach (var article in new[] { "the ", "a ", "an " })
        if (v.StartsWith(article)) { v = v[article.Length..]; break; }
    // Remove common punctuation
    v = Regex.Replace(v, @"[/\-'\"",.]", "");
    v = Regex.Replace(v, @"\s+", " ").Trim();
    return v;
}
```

### OSA distance with early-exit

```csharp
internal static int OsaDistance(string a, string b, int earlyExitThreshold)
{
    if (Math.Abs(a.Length - b.Length) > earlyExitThreshold) return earlyExitThreshold + 1;
    // Standard two-row rolling array OSA implementation ...
    // Return earlyExitThreshold + 1 as soon as the running minimum exceeds it.
}
```

### Duplicate detection pipeline

```csharp
// 1. Exact duplicates
var exactGroups = FindExactDuplicateGroups(songs);
var exactIds = exactGroups.SelectMany(g => g).Select(s => s.Id).ToHashSet();

// 2. Likely duplicates (O(n²))
var likelyGroups = FindLikelyDuplicateGroups(songs, exactIds);

// 3. Build CSV
var sb = new StringBuilder("Artist;Title;FilePath\n");
foreach (var group in exactGroups.Concat(likelyGroups))
    foreach (var song in group)
        sb.AppendLine($"{EscapeCsvField(song.Artist)};{EscapeCsvField(song.Title)};{EscapeCsvField(song.FullPath ?? "")}");
```

---

## Step 3 — `Export.razor`

Key skeleton:

```razor
@page "/export"
@using Karamel.Web.Helpers
@using Karamel.Web.Contracts
@using Karamel.Web.Models
@implements IAsyncDisposable
@inject IJSRuntime JSRuntime

<PageTitle>Export Song List - Karamel Karaoke</PageTitle>

<!-- Select Folder button — always visible -->
<button class="btn k-btn-outline btn-lg w-100" @onclick="SelectFolder" disabled="@_isScanning">
    @if (_isScanning) { <span class="spinner-border spinner-border-sm"></span> <span> Scanning…</span> }
    else { <span>📁 Select Folder</span> }
</button>

@if (_scanError != null) { <div class="alert alert-danger">@_scanError</div> }

<!-- Download buttons — hidden until scan complete -->
@if (_scanComplete && !_isScanning)
{
    <div class="mt-3 d-flex gap-2 flex-wrap">
        <button class="btn k-btn-primary" @onclick="DownloadArtists">⬇ Download Artists</button>
        <button class="btn k-btn-primary" @onclick="DownloadTitles">⬇ Download Titles</button>
        <button class="btn k-btn-primary" @onclick="DownloadDuplicates">⬇ Download Duplicates</button>
    </div>
    <p class="text-muted mt-2">@_songs!.Count songs loaded.</p>
}

@code {
    private IJSObjectReference? _module;
    private List<Song>? _songs;
    private bool _isScanning;
    private bool _scanComplete;
    private string? _scanError;

    private const string FilenamePattern = "%artist - %title";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/exportBridge.js");
    }

    private async Task SelectFolder()
    {
        _isScanning = true;
        _scanError = null;
        StateHasChanged();
        try
        {
            var dtos = await _module!.InvokeAsync<SongDto[]>("scanDirectory", FilenamePattern);
            _songs = dtos.Select(SongConverters.ConvertDtoToSong).ToList();
            _scanComplete = true;
        }
        catch (JSException ex) when (ex.Message.Contains("AbortError"))
        {
            // User cancelled picker — silently ignore
        }
        catch (Exception ex)
        {
            _scanError = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _isScanning = false;
            StateHasChanged();
        }
    }

    private async Task DownloadArtists()
    {
        var csv = CsvExportHelper.GenerateArtistsCsv(_songs!);
        await _module!.InvokeVoidAsync("triggerDownload", csv, "artists.csv");
    }

    private async Task DownloadTitles()
    {
        var csv = CsvExportHelper.GenerateTitlesCsv(_songs!);
        await _module!.InvokeVoidAsync("triggerDownload", csv, "titles.csv");
    }

    private async Task DownloadDuplicates()
    {
        var csv = CsvExportHelper.GenerateDuplicatesCsv(_songs!);
        await _module!.InvokeVoidAsync("triggerDownload", csv, "duplicates.csv");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
```

---

## Step 4 — Testing

### C# Tests (`ExportPageTests.cs`)

Test scope:
1. `Export.razor` renders without session parameter (US4)
2. Download buttons hidden before scan, visible after scan (FR-004)
3. Scan error displays alert (error path)
4. `CsvExportHelper.GenerateArtistsCsv` — header row present
5. `CsvExportHelper.GenerateArtistsCsv` — sorted correctly (digits first, A-Z after)
6. `CsvExportHelper.GenerateTitlesCsv` — header `Title;Artist`, sorted by title
7. `CsvExportHelper.GenerateDuplicatesCsv` — header only when no duplicates
8. `CsvExportHelper.GenerateDuplicatesCsv` — exact duplicates listed first
9. `CsvExportHelper.GenerateDuplicatesCsv` — likely duplicates after exact
10. `CsvExportHelper.EscapeCsvField` — semicolons quoted
11. `CsvExportHelper.NormalizeForComparison` — article stripping, punctuation removal
12. `CsvExportHelper.OsaDistance` — threshold early-exit

### JS Tests (`exportBridge.test.js`)

Test scope:
1. `triggerDownload` creates Blob with correct MIME type
2. `triggerDownload` sets correct filename on anchor element
3. `triggerDownload` calls `URL.revokeObjectURL` after click (cleanup)
4. `scanDirectory` delegates to `pickLibraryDirectory` with correct arguments
5. `scanDirectory` propagates thrown error on picker cancel

---

## Build & Test Commands

```powershell
# Build check
dotnet build

# C# tests (targeted — run only export tests first)
dotnet test Karamel.Web.Tests --filter "FullyQualifiedName~ExportPage"

# Full C# test suite
dotnet test Karamel.Web.Tests

# JavaScript tests
cd Karamel.Web/wwwroot
npm run test:run
cd ../..
```

---

## What is NOT in scope

- No navigation link to `/export` from any existing page (FR-002)
- No session creation on `/export` (FR-003)
- No Fluxor state changes (FR-003b)
- No backend API calls
- No singerName / session / playlist data in any CSV
- No FilePath transmitted to backend (stays in local browser download only)
