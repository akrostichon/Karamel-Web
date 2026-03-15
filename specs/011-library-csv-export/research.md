# Research: Library CSV Export

**Branch**: `feature/011-library-csv-export` | **Date**: 2026-03-15

## R-001: Levenshtein Distance Thresholds for Duplicate Detection

**Decision**: Fixed thresholds — Artist: **2**, Title: **3** — applied after preprocessing.

**Rationale**:
- The AND-combination (artist AND title both within threshold) eliminates virtually all false positives at these library sizes.
- For 5,000 songs, every pair with similar artist names is approximately 1-in-1,000; requiring the title to also be within threshold reduces false positives to ~1-in-1,000,000.
- Fixed thresholds are easy to document per FR-015.
- A hybrid relative formula (e.g., `max(2, floor(len × 0.1))`) adds complexity for marginal benefit in this domain.

**Preprocessing (applied before comparison)**:
1. Lowercase
2. Strip leading definite/indefinite articles: `"the "`, `"a "`, `"an "`
3. Normalize common punctuation: remove `/`, `-`, `'`, `,`, `.` (collapse to space)
4. Collapse multiple spaces to single space
5. Trim

This preprocessing alone eliminates the majority of real-world catalog duplicates (`The Beatles` / `Beatles`, `AC/DC` / `ACDC`, `Beyoncé` / `Beyonce`) before any distance computation.

**Alternatives considered**:
- Relative threshold (10% of length): rejected — too strict for short strings (3-char artist names → threshold 0), threshold-0 means only exact match.
- Sliding-window O(n·k) algorithm: rejected — misses 5–15% of duplicates where article stripping or abbreviation expansion changes sort position. 100% coverage is required for an export that operators will use to clean their library.

---

## R-002: Algorithm Selection for Likely-Duplicate Detection

**Decision**: **O(n²) exhaustive pair comparison** with early-exit optimization, reusing the **OSA (Optical String Alignment / restricted Damerau-Levenshtein) algorithm** implemented from scratch in `CsvExportHelper.cs`.

**Rationale**:
- `FuzzySearchService.ComputeOsaDistance` exists in `Karamel.Backend` but is not referenced by `Karamel.Web` and cannot be easily shared without a new project or code duplication. The OSA algorithm is ~20 lines; duplicating it in `CsvExportHelper.cs` is the simplest and most self-contained approach.
- O(n²) exhaustive search: 5,000 songs → 12.5M pairs. With early-exit (bail when running cost exceeds threshold), average ~15 ops/pair → ~187M total ops → **~200ms in C# WASM**. Well within the 5-second budget (SC-005).
- No NuGet packages required (consistent with project policy).

**Early-exit optimization**:
- Step 0: skip pair if both songs are already in an exact-duplicate group
- Step 1: if `|len(normalizedArtist1) - len(normalizedArtist2)| > artistThreshold` → skip
- Step 2: run OSA on artists with early-exit at `artistThreshold` → skip if exceeds
- Step 3: if `|len(normalizedTitle1) - len(normalizedTitle2)| > titleThreshold` → skip
- Step 4: run OSA on titles with early-exit at `titleThreshold` → add to candidates

**Alternatives considered**:
- Sliding window O(n·k): rejected (see R-001).
- BK-tree (Burkhard-Keller tree): would provide O(n log n) lookup but requires O(n) build time and adds significant implementation complexity for a one-time in-memory operation.
- .NET BCL `FuzzyMatcher`: does not exist.

---

## R-003: CSV Generation and Download Approach

**Decision**: Generate CSV as a UTF-8 `string` in C# (`CsvExportHelper.cs`), pass the string to `exportBridge.js` via `IJSObjectReference.InvokeVoidAsync("triggerDownload", content, filename)`, which creates a `Blob`, a temporary object URL, and triggers a click on a hidden `<a>` element.

**Rationale**:
- String-based CSV generation in C# is simple, testable without JS interop, and allows `CsvExportHelper` to be unit-tested entirely in xUnit without bUnit overhead.
- The `Blob + <a click>` download pattern is well-established for browser-initiated downloads and requires no server round-trip.
- Existing JS modules (`homeInterop.js`, `fullscreen.js`) demonstrate the established pattern of thin JS wrappers around browser APIs.

**Quoting rule** (RFC 4180 compliant, adapted for semicolons):
- Wrap a field in double-quotes if it contains: `;` (semicolon), `"` (double-quote), `\n` (newline), or `\r` (carriage return).
- Escape internal double-quotes by doubling: `"` → `""`.
- Empty fields: output as empty string (no quotes required unless explicitly empty within a quoted context).

**Alternatives considered**:
- Server-side generation: rejected — contradicts FR-013 (on-demand, no server-side file storage).
- Streaming large files via byte arrays: unnecessary for ≤5,000 songs; even 5,000 rows × 80 chars/row = 400KB, well within browser memory.
- Existing CsvHelper NuGet: rejected — project prefers no new dependencies for a simple, constrained use case.

---

## R-004: JS Interop Pattern for Directory Scanning

**Decision**: `exportBridge.js` is a new ES module that **statically imports** `pickLibraryDirectory` from `./fileAccess.js` and exposes:
1. `scanDirectory(filenamePattern)` — calls `pickLibraryDirectory(filenamePattern)` (default progressStep)
2. `triggerDownload(content, filename)` — creates `Blob` → object URL → `<a>` click → revoke URL

**Rationale**:
- Static import is consistent with how other modules import from `fileAccess.js` and `logger.js`.
- Reuses the existing `pickLibraryDirectory` function verbatim — no duplication, no wrapper that could drift from the Home page scan behavior.
- No DotNet progress callback is required for Export (spec requires only a spinner, not incremental count updates). `exportBridge.scanDirectory()` is simply awaited; Blazor renders the spinner during the `await`.
- `fileAccess.js`'s `libraryDirectoryHandle` is a module-scope variable that could theoretically be overwritten if a user navigates from an active Home session to `/export` in the same tab. This is acceptable: (a) `/export` is not linked from any UI; (b) if a user manually navigates there, the export's directory scan is intentionally independent.

**Alternatives considered**:
- Reuse `homeInterop.js`'s `selectLibrary()` export: rejected — it sets up a DotNet progress event bridge that the Export page doesn't need, coupling two unrelated page flows.
- Dynamically import `fileAccess.js` at call time: rejected — unnecessary complexity; static imports are resolved once at module load.

---

## R-005: Sorting Order

**Decision**: Use `StringComparer.Ordinal` for case-normalized (lowercased) keys to achieve "digits and special characters before A-Z" ordering.

**Rationale**:
- Spec requires: special characters and digits first, then A-Z, case-insensitive.
- `StringComparer.OrdinalIgnoreCase` would also sort digits/specials before letters but does case-folding internally. To avoid surprises, we normalize to lowercase first and then use `StringComparer.Ordinal`. Ths gives: `!` (U+0021), `0-9` (U+0030–0039), `A-Z` (U+0041–005A = letters after digits in Unicode).
- `StringComparer.InvariantCulture` sorts differently (e.g., some locales sort `é` with `e`, placing it between `e` entries — not what we want for a raw ASCII-first sort).

**Sort key derivation**: `(song.Artist ?? "").ToLowerInvariant()` for artist sort; `(song.Title ?? "").ToLowerInvariant()` for title sort.

---

## R-006: Grouping Logic for Likely Duplicates

**Decision**: After finding all likely-duplicate *pairs*, cluster them using **Union-Find (Disjoint Set Union)** to build groups.

**Rationale**:
- Likely-duplicate pairs form a graph where edges = "this pair is within Levenshtein threshold." Two songs A, B, C may form one group if A≈B and B≈C even if A≈C is not directly confirmed.
- Union-Find runs in O(n α(n)) ≈ O(n) for n songs — essentially free.
- Alternative: greedy scan (add B to A's group if similar to any member) — simpler but group membership can depend on iteration order.
- Union-Find is the standard, deterministic approach.

**Group output**: A group is only output if it has ≥2 members. Groups with a single entry are discarded (no duplicate to report).
