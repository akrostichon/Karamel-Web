# ZIP Support Plan for Karamel-Web

Date: 2026-01-10

This document describes the plan to add support for ZIP-based karaoke files (paired `.mp3` + `.cdg`) to Karamel-Web.

Goals
- Detect `.zip` files during library scanning and enumerate their entries.
- Treat ZIP contents as a virtual directory and detect paired `.mp3` + `.cdg` files that share the same basename and are in the same inner-folder (Option A).
- Lazily extract the matching `.mp3` and `.cdg` entries into in-memory buffers (ArrayBuffer/Blob) when playback is requested.
- Feed extracted CDG ArrayBuffer to CDGraphics.js and MP3 Blob/object URL to the existing player flow so no change to renderer is necessary.
- Keep extraction in memory only (no writes to disk); maintain main-tab extraction restriction.
- Do not surface ZIP origin to users in the UI.

High-level approach
1. Song metadata and state
   - Extend `Karamel.Web/Models/Song.cs` to include new fields:
     - `SourceType` (string enum: `directory` | `zip`)
     - `ZipFileName` (string)
     - `ZipEntryMp3Path` (string)
     - `ZipEntryCdgPath` (string)
   - Update library state (store actions/reducers) to preserve these fields in snapshots exchanged across tabs.

2. Dependency and licensing
   - Use JSZip (MIT) for in-browser ZIP listing and extraction.
   - Runtime: dynamic CDN import (e.g., `https://cdn.jsdelivr.net/npm/jszip@latest/dist/jszip.min.js`).
   - Dev/tests: add `jszip` to `Karamel.Web/wwwroot/package.json` devDependencies for Vitest.
   - Add an entry to `THIRD-PARTY-NOTICES.md` mentioning JSZip and include a link to the MIT license.

3. Scanning (no extraction)
   - Update `Karamel.Web/wwwroot/js/fileAccess.js` scanning logic:
     - Detect files ending with `.zip` while traversing the directory.
     - For each zip file: read its ArrayBuffer via the file handle and call `JSZip.loadAsync(arrayBuffer)`.
     - Enumerate entries; build maps of `.mp3` and `.cdg` entries grouped by inner-folder path and basename.
     - For each matched pair where both files are in the same inner-folder and share a basename, add a `Song` object to the library with ZIP metadata populated. DO NOT extract contents yet.
   - Keep existing behavior for files on the filesystem (unchanged).

4. Lazy extraction at playback
   - Update loader `loadSongFiles` (or equivalent) in `fileAccess.js`:
     - If `Song.SourceType === 'zip'`:
       - Read the zip file handle: `const zipBuf = await zipHandle.getFile().arrayBuffer()`.
       - `const zip = await JSZip.loadAsync(zipBuf)`.
       - Extract cdg entry as ArrayBuffer: `await zip.file(zipEntryCdgPath).async('arraybuffer')`.
       - Extract mp3 entry as Blob or ArrayBuffer and create an object URL: `URL.createObjectURL(new Blob([mp3ArrayBuffer], { type: 'audio/mpeg' }))`.
       - Return the same structure the player currently expects: CDG ArrayBuffer and MP3 object URL / Blob.
     - Ensure object URL revocation when playback ends.

5. Metadata extraction
   - Update `Karamel.Web/wwwroot/js/metadata.js` to accept `Blob`/`ArrayBuffer` inputs.
   - When metadata is required for a zip-origin song, wrap the extracted mp3 ArrayBuffer in a `Blob` or `File` and pass it to `jsmediatags` for ID3 parsing. Fallback to filename parsing as before.

6. Player compatibility
   - `Karamel.Web/wwwroot/js/player.js` already constructs `CDGraphics` with an ArrayBuffer and uses object URLs for MP3. No core change required; ensure the loader returns data in the same format.
   - Add error handling for missing entries, corrupted ZIPs, or large files.

7. Tests
   - Add unit tests (Vitest) under `Karamel.Web/wwwroot/js` to cover ZIP scanning and lazy extraction flows. Use small fixture ZIPs in `Karamel.Web/wwwroot/test-fixtures/zip/`.
   - Adjust `Karamel.Web.Tests` where mocks expect only directory-origin songs to accept the new `Song` shape for zip-origin.

8. Documentation
   - Update `README.md` and `TESTING_STRATEGY.md` to document ZIP behavior, limitations, and test fixtures.

Implementation details and notes
- Nested paths: this implementation requires both `.mp3` and `.cdg` to be in the same inner-folder inside the ZIP (Option A). This reduces false matches and matches existing on-disk expectations.
- Performance: ZIP files will only be loaded and their matched entries extracted when the user requests playback of a ZIP-origin song (lazy). The scanner reads the central directory using JSZip for matching, but does not extract audio/cdg streams during scan.
- Security: Pin `jszip` to a known version in `package.json` for tests and note the CDN version used. Validate file sizes before fully loading very large entries to avoid OOM.
- License: JSZip is MIT-licensed; add an entry to `THIRD-PARTY-NOTICES.md` and include the license link. No additional obligations beyond retaining the license notice are required.

Next actions (implementation)
1. Add a tracked TODO list and write this plan to the repository root (done).
2. Add `jszip` to `Karamel.Web/wwwroot/package.json` and update `THIRD-PARTY-NOTICES.md`.
3. Modify `Karamel.Web/Models/Song.cs` and library state to carry ZIP metadata.
4. Update `Karamel.Web/wwwroot/js/fileAccess.js` scanner and loader for ZIP handling.
5. Update `metadata.js` and verify `player.js` integration.
6. Add tests and fixtures, update documentation, run JS tests, then request manual C# tests per repo guidance.

If you want, I can now implement steps 2 and 3 (package + model changes) and add the test fixtures. Confirm and I will proceed.
