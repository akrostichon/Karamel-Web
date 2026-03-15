# Feature Specification: Library CSV Export

**Feature Branch**: `feature/011-library-csv-export`
**Created**: 2026-03-15
**Status**: Draft
**Input**: User description: "REQ-4.1-REQ-4.6: A standalone /export page (no session required, no UI links) with three independently downloadable CSV files: artist-sorted list (artists.csv), title-sorted list (titles.csv), and a duplicates list (duplicates.csv) with exact- and fuzzy-duplicate detection using Levenshtein distance. All files UTF-8, semicolon-separated, with header rows."

## Clarifications

### Session 2026-03-15

- Q: Where does the `/export` page read library data from? → A: It provides its own directory scan (same File System Access API flow as Home page) and works entirely on local component state — independent of Fluxor `LibraryState` and any active session.
- Q: How is Levenshtein distance applied for likely-duplicate detection? → A: Artist and Title are compared **separately** — both must independently fall within their own threshold for entries to be classified as likely duplicates.
- Q: What is the state of the download buttons while a directory scan is in progress? → A: The three download buttons are **hidden** during scanning and only appear once the scan completes.
- Q: What feedback is shown to the operator while the directory scan is running? → A: A simple spinner with "Scanning…" text — no song count or progress detail is shown during the scan. Once the scan completes, the total song count is displayed.
- Q: What happens when the operator selects a new folder after a scan has already completed? → A: The "Select Folder" button remains visible after scan; selecting a new folder replaces the previous results entirely.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Scan Directory and Download Artist-Sorted Song List (Priority: P1)

An operator navigates directly to `/export` in their browser. The page presents a "Select Folder" button. After selecting a music directory, the page scans it (same File System Access API mechanism as the Home page) and loads the song list into its own local state. The operator then clicks "Download Artists" to immediately generate and download `artists.csv` — a semicolon-separated file sorted alphabetically by Artist (special characters and digits first, then A-Z, case-insensitive), with columns `Artist;Title`. A header row is always present.

**Why this priority**: This is the core export use case. Operators most commonly need an alphabetical artist index to hand out to singers.

**Independent Test**: Can be tested by navigating to `/export`, selecting a folder, waiting for the scan to complete, clicking "Download Artists", and verifying the downloaded file's contents, column order, sort order, and encoding independently of the other export functions.

**Acceptance Scenarios**:

1. **Given** the operator has selected and scanned a directory with songs in random order, **When** the operator clicks "Download Artists", **Then** a file named `artists.csv` is downloaded that contains a header row `Artist;Title`, followed by all songs sorted A-Z by Artist (case-insensitive, special characters and digits first), UTF-8 encoded, with semicolons as delimiters.
2. **Given** a scan has completed, **When** the downloaded `artists.csv` is opened in a text editor, **Then** the first line is exactly `Artist;Title`.
3. **Given** songs with leading digits or special characters exist in the scanned directory, **When** the file is generated, **Then** those songs appear before A-Z entries in the Artist column.
4. **Given** no directory has been selected yet or a scan is in progress, **When** the operator views the page, **Then** the three download buttons are not visible; they appear only after the scan completes successfully.

---

### User Story 2 - Download Title-Sorted Song List (Priority: P1)

After scanning a directory (see User Story 1), the operator clicks "Download Titles". The page generates and downloads `titles.csv` — a semicolon-separated file containing all scanned songs sorted alphabetically by Title (same sort rules as the artist list), with columns `Title;Artist`.

**Why this priority**: Equal priority to artist list — operators need both sorted views to assist singers searching by song title.

**Independent Test**: Can be tested by completing a directory scan on `/export`, clicking "Download Titles", and verifying the download file name, column order (`Title;Artist`), sort order, and encoding.

**Acceptance Scenarios**:

1. **Given** a directory scan has completed, **When** the operator clicks "Download Titles", **Then** a file named `titles.csv` is downloaded with header `Title;Artist`, songs sorted A-Z by Title (case-insensitive, specials/digits first), UTF-8 encoded, semicolons as delimiters.
2. **Given** the scanned directory contains no recognised songs, **When** the operator clicks "Download Titles", **Then** `titles.csv` is downloaded containing only the header row.
3. **Given** songs with identical titles but different artists, **When** sorted, **Then** those songs appear consecutively; tie-breaking order is not specified but must be stable.

---

### User Story 3 - Download Duplicates Report (Priority: P2)

After scanning a directory (see User Story 1), the operator clicks "Download Duplicates". The page generates and downloads `duplicates.csv` — a semicolon-separated file listing all detected duplicate entries from the scanned library. The file has columns `Artist;Title;FilePath`. Exact duplicates (same Artist+Title, case-insensitive) appear first; likely duplicates (Artist+Title within a Levenshtein distance threshold suggesting a probable typo) appear after. Entries belonging to the same duplicate group are grouped in consecutive rows. If no duplicates exist, only the header row is present.

**Why this priority**: Duplicate detection is a quality-of-life feature; the core download functionality (stories 1 and 2) is more critical.

**Independent Test**: Can be tested by loading a library with known duplicate and near-duplicate entries, downloading `duplicates.csv`, and verifying grouping, ordering, column layout, and that non-duplicates are absent.

**Acceptance Scenarios**:

1. **Given** a library containing two songs with identical Artist and Title (case-insensitive), **When** the operator clicks "Download Duplicates", **Then** `duplicates.csv` lists both songs consecutively in the "exact duplicates" section with all three columns populated.
2. **Given** a library containing two songs whose Artist+Title strings differ by a small number of characters (within the defined Levenshtein threshold), **When** the file is generated, **Then** both appear consecutively in the "likely duplicates" section.
3. **Given** a library with no duplicates, **When** the operator clicks "Download Duplicates", **Then** `duplicates.csv` contains only the header row `Artist;Title;FilePath`.
4. **Given** a library with both exact and likely duplicates, **When** the file is generated, **Then** exact duplicate groups appear before likely duplicate groups.
5. **Given** a library where one song appears in three identical copies, **When** the file is generated, **Then** all three copies are grouped consecutively in a single group.

---

### User Story 4 - Access Export Page Without Session (Priority: P1)

An operator visits `/export` without any active karaoke session. The page loads successfully and shows a "Select Folder" button. No session is created, no session parameter is required, and no session-related error is shown. All library data lives in local page state and is never persisted to the backend or to `LibraryState`.

**Why this priority**: The export feature must be fully session-independent and self-contained; this is a fundamental architectural requirement (REQ-4.1).

**Independent Test**: Can be tested by navigating directly to `/export` with no session query parameter and verifying the page renders the "Select Folder" button without errors or redirects, and that no session object is created in the backend during the entire workflow.

**Acceptance Scenarios**:

1. **Given** no session is active, **When** the operator navigates to `/export`, **Then** the page loads and displays a "Select Folder" button without any error or session-creation prompt.
2. **Given** a session URL parameter is present, **When** the operator navigates to `/export?session=...`, **Then** the session parameter is silently ignored; the page behaves identically.
3. **Given** the operator scans a directory and downloads all three CSVs, **When** the backend session list is checked, **Then** no new session has been created.

---

### Edge Cases

- What happens when the library contains a song with semicolons in the Artist or Title? The field must be quoted per CSV conventions to avoid column corruption.
- What happens when a song has a missing Artist or Title? The field is output as empty (blank string between semicolons); the row is not omitted.
- What happens when FilePath contains special characters or semicolons? Same quoting rule as Artist/Title.
- What happens when two songs are near-duplicates but one is also an exact duplicate with a third? Exact-duplicate grouping takes priority; the entry appears only in the exact-duplicates section.
- What happens when the Levenshtein threshold yields an unreasonably large number of "likely duplicates" for a large library? The threshold value documented during implementation must be validated against a representative library sample.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a dedicated page at the URL `/export` that is accessible directly via the browser address bar without any session parameter.
- **FR-002**: The `/export` page MUST NOT be linked or referenced in any application UI navigation (menus, buttons, home page, etc.).
- **FR-003**: Accessing `/export` MUST NOT create or start a karaoke session.
- **FR-003a**: The `/export` page MUST provide a "Select Folder" button that invokes the File System Access API directory picker (same mechanism as the Home page) to scan a local music directory into page-local state.
- **FR-003b**: The scanned song data MUST be stored in local component state only — it MUST NOT be written to Fluxor `LibraryState`, sessionStorage, or the backend.
- **FR-003c**: While a directory scan is in progress, the page MUST display a spinner and the text "Scanning…". No song count or other progress detail is shown during an active scan. Once the scan completes, the total song count of the scanned library MUST be displayed.
- **FR-003d**: The "Select Folder" button MUST remain visible after a scan completes. Selecting a new folder MUST replace all previous scan results in component-local state; no confirmation is required.
- **FR-004**: After a successful scan, the `/export` page MUST reveal three download buttons: one for the artist-sorted list, one for the title-sorted list, and one for the duplicates list. The buttons MUST be hidden (not rendered) before a scan has completed and while a scan is in progress.
- **FR-005**: Clicking "Download Artists" MUST generate and immediately trigger a browser download of a file named `artists.csv` containing all library songs sorted by Artist ascending (special characters and digits before A-Z, case-insensitive), with columns `Artist;Title` and a header row.
- **FR-006**: Clicking "Download Titles" MUST generate and immediately trigger a browser download of a file named `titles.csv` containing all library songs sorted by Title ascending (same sort order as FR-005), with columns `Title;Artist` and a header row.
- **FR-007**: Clicking "Download Duplicates" MUST generate and immediately trigger a browser download of a file named `duplicates.csv` with columns `Artist;Title;FilePath` and a header row.
- **FR-008**: `duplicates.csv` MUST list exact duplicates (entries sharing identical Artist+Title, case-insensitive) first, followed by likely duplicates. A likely duplicate is defined as two or more entries where the Artist values are within a defined Levenshtein distance threshold **and** the Title values are independently within their own defined Levenshtein distance threshold.
- **FR-009**: Duplicate entries within the same group MUST appear in consecutive rows in `duplicates.csv`.
- **FR-010**: If no duplicates are detected, `duplicates.csv` MUST contain only the header row.
- **FR-011**: All three CSV files MUST be encoded in UTF-8.
- **FR-012**: All three CSV files MUST use semicolons (`;`) as field delimiters.
- **FR-013**: Each download operation MUST generate the file at the moment of the button click, based on the locally scanned song list. No server-side file storage is required.
- **FR-014**: CSV fields containing semicolons MUST be quoted to prevent column corruption.
- **FR-015**: The Levenshtein distance threshold used for likely-duplicate detection MUST be defined and documented during implementation.

### Key Entities

- **Library Song**: A song in the currently loaded library, with attributes: Artist (string), Title (string), FilePath (string, may be empty for non-main-tab contexts).
- **Exact Duplicate Group**: Two or more Library Songs sharing identical Artist+Title values (case-insensitive comparison).
- **Likely Duplicate Group**: Two or more Library Songs where the Artist values are within a defined Levenshtein distance threshold AND the Title values are independently within their own defined Levenshtein distance threshold, and that are not already classified as exact duplicates.
- **CSV Export File**: A UTF-8 encoded, semicolon-delimited text file with a header row, generated on-demand from the current library state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator can navigate to `/export` and click any of the three download buttons within 10 seconds of the page loading, with no session setup required.
- **SC-002**: Each downloaded CSV file contains a correct header row and all library songs — operators can verify this by opening the file in a spreadsheet application without encountering encoding or delimiter errors.
- **SC-003**: The artist-sorted and title-sorted files are sorted correctly such that 100% of test cases with known input produce correctly ordered output (special characters/digits before A-Z, case-insensitive).
- **SC-004**: The duplicates file correctly identifies all exact duplicates in a test library of 100 songs, with zero false negatives for exact matches.
- **SC-005**: Downloading all three files takes under 5 seconds each for a library of up to 5,000 songs on a standard laptop.
- **SC-006**: The `/export` page is not discoverable via any in-app navigation link, verified by a manual UI walkthrough of all pages.

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any X must be justified before the spec is approved.
> Full principles: [Karamel-Web Constitution](.specify/memory/constitution.md)

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: The `/export` page requires no session, no QR code, and no filesystem access — it works on any device that can navigate to the URL directly.
- [x] **Backend as source of truth**: Not applicable — the export page scans its own local directory and stores results in component-local state only. It intentionally does not read from the backend or from `LibraryState`.
- [x] **Session ID from backend**: Not applicable — this feature is explicitly session-independent.
- [x] **Session parameter validated**: The `/export` page is explicitly exempt from session validation per REQ-4.1; it silently ignores any session parameter.

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: `duplicates.csv` includes FilePath for duplicate identification purposes. File paths are only present in the main tab's library state and are written into the local download file — they are never transmitted to any server or third party. NOTE: FilePath is required by REQ-4.4 for duplicate identification; it is written to a local file on the operator's machine, not sent to any backend.
- [x] **Minimal data**: The export files contain only song metadata (Artist, Title, FilePath). No personal data (singer names, session history) is included.
- [x] **Consent-gated telemetry**: No new telemetry events are introduced by this feature.
- [x] **No sensitive logging**: No passwords, tokens, or PII are logged.
