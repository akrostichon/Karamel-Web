# Karamel-Web Development Plan

## Current Status
✅ **Prototype Complete** - Basic MP3+CDG playback working with File System Access API and CDGraphics.js integration

## Project Context

### User Types
1. **Admin (main session)**: Loads session, selects karaoke library folder, shows player
2. **Admin (second screen/device)**: Manages playlist with drag-and-drop
3. **Singer**: Enters name, searches library, adds songs to queue

### Technical Stack
- Frontend: Blazor WebAssembly (.NET 10.0)
- CDG Rendering: cdgraphics.js v7.0.0 (ES module from CDN)
- File Access: File System Access API (Chrome/Edge only)
- Future Backend: ASP.NET Core Web API + Azure SQL (not in this phase)

### Key Decisions
- **Session scope**: Session = from folder selection to browser close. All data (library, playlist) is session-scoped.
- **No persistence**: User re-selects library folder at start of each session. No localStorage for library or playlist.
- **Song identification**: ID3 tags preferred, fallback to "Artist - Song" filename parsing.
- **Browser support**: Chrome/Edge only (File System Access API requirement).

---
## Implementation rules
- if current branch is main, always create a feature branch and switch to it, before you start implementing.
- implement unit tests for non-ui logic
- do not execute the merge back to main branch yourself. push the changes on the feature branch.
---

## Phase 1: Prototype Cleanup ✅ COMPLETE

### Step 1.1: Remove Template Files ✅
**Delete**:
- ✅ `Pages/Counter.razor`
- ✅ `Pages/Weather.razor`

**Update**:
- ✅ `Layout/NavMenu.razor` - Remove Counter/Weather links
---

## Phase 2: Library Management & Multi-View Architecture

### Step 2.1: Directory Scanning ✅
**Files**: `wwwroot/js/fileAccess.js`, `wwwroot/js/fileAccess.test.js`

- ✅ Add `pickLibraryDirectory()` function using `showDirectoryPicker()`
- ✅ Recursively scan directory for `.mp3` files
- ✅ For each MP3, look for matching `.cdg` file (same name, different extension)
- ✅ Return array of song metadata objects
- ✅ **Keep directory handle in JavaScript module scope** for session-long file access
- ✅ Added `loadSongFiles()` function for loading specific songs during playback
- ✅ **Unit tests with Vitest** - 14 tests covering all functionality with mocked File System Access API

### Step 2.2: Song Metadata Extraction ✅
**Files**: `wwwroot/js/fileAccess.js`, `wwwroot/js/metadata.js`

- Import jsmediatags from CDN: `https://cdn.jsdelivr.net/npm/jsmediatags@3.9.5/+esm`
- Extract ID3 tags: artist, title
- **Fallback**: If no ID3 tags, parse filename using configurable pattern (default: "%artist - %title")
- Return song metadata array (no file handles - those stay in JS for playback)
- Unit tests with Vitest
- **Status**: ✅ COMPLETED (commit: 0e5fe7e)

### Step 2.3: State Management Setup ✅
**Files**: `Models/Song.cs`, `Models/Session.cs`, `Store/LibraryState.cs`, `Store/PlaylistState.cs`

- ✅ Install NuGet: `Fluxor.Blazor.Web`
- ✅ Create `Song` model: Id (GUID), Artist, Title, Mp3FileName, CdgFileName, AddedBySinger
- ✅ Create `Session` model: SessionId (GUID), CreatedAt, LibraryPath, RequireSingerName, PauseBetweenSongs, FilenamePattern
- ✅ Create `LibraryState`: List<Song>, loading status, search filter (sorted alphabetically by Artist, then Title)
- ✅ Create `PlaylistState`: Queue<Song> (queue first item is next song), current Song + SingerName, Dictionary<string, int> SingerSongCounts
- ✅ Define actions: LoadLibrary, FilterSongs, AddToPlaylist (validates 10-song limit), RemoveSong, ReorderPlaylist, NextSong (pops first item)
- ✅ Configure Fluxor in Program.cs and App.razor
- **Status**: ✅ COMPLETED

### Step 2.4: Session Sharing Mechanism ✅
**Files**: `wwwroot/js/sessionBridge.js`, `wwwroot/js/sessionBridge.test.js`, `Services/SessionService.cs`
- ✅ Generate session URL with SessionId as query parameter: `/session?id={guid}`
- ✅ Use **Broadcast Channel API** for cross-tab state synchronization:
  - Main tab (with folder access) broadcasts playlist changes (library will be stable)
  - Secondary tabs (Playlist, Singer views) listen and update their state
- ✅ **sessionStorage**: Persist session state (library metadata, playlist, settings) so tab refresh doesn't lose context
- ✅ Create sessionBridge.js to handle broadcast messages
- ✅ SessionService.cs manages session state in Fluxor and triggers JS broadcast
- ✅ **Multiple Sessions Support**: Each session has its own isolated state using session-specific storage keys and broadcast channels
  - Different browser tabs/windows can run independent karaoke sessions simultaneously
  - Sessions are identified by unique GUIDs ensuring no cross-contamination
  - Each session has its own directory handle, library, and playlist state
- ✅ **Unit tests with Vitest** - Comprehensive test coverage for session isolation, broadcast messaging, and state synchronization
- ✅ **Note**: Main tab must remain open (holds directory handle). If closed, session ends for all tabs.
- **Status**: ✅ COMPLETED

### Step 2.5: Home Page (Session Initialization) ✅
**Files**: `Pages/Home.razor`, `wwwroot/js/homeInterop.js`, `wwwroot/js/homeInterop.test.js`

- ✅ Replace template content with session initialization UI
- ✅ "Select Karaoke Library" button (calls pickLibraryDirectory)
- ✅ Checkbox: Allow singers to reorder playlist (default: unchecked)
- ✅ Checkbox: Require singer name (default: checked)
- ✅ Textbox: Seconds pause between songs (default: 5)
- ✅ Textbox: Filename parsing pattern (default: "%artist - %title")
- ✅ Browser compatibility warning (File System Access API required)
- ✅ "Start Karaoke Session" button:
  - Disabled until library selected
  - Creates session GUID, saves settings to state
  - Opens Playlist view in new tab: `/playlist?session={guid}`
  - Opens Singer view in new tab: `/singer?session={guid}`
  - **Current tab navigates to NextSongView** (retains folder access)
- ✅ **Testing**: Extract JavaScript logic to homeInterop.js module with comprehensive Vitest unit tests (see [TESTING_STRATEGY.md](TESTING_STRATEGY.md) for details)
- **Status**: ✅ COMPLETED

### Step 2.6: Library Search Component ✅
**Files**: `Components/LibrarySearch.razor`, `Karamel.Web.Tests/LibrarySearchTests.cs`

#### Implementation:
- ✅ Reusable component displaying scanned songs in searchable table
- ✅ Columns: Artist, Title, Actions
- ✅ Search box filters by artist/title (client-side, case-insensitive, contains)
- ✅ "Add to Queue" button (microphone icon) dispatches AddToPlaylist action
- ✅ Shows confirmation toast on successful add

#### Testing:
- ✅ **Unit tests**: Test filtering logic (case-insensitive, contains match)
- ✅ **Unit tests**: Verify table displays correct columns with proper sorting (alphabetically by Artist, then Title)
- ✅ **Unit tests**: Test AddToPlaylist action dispatch with correct song data
- ✅ **Unit tests**: Test toast notification display on successful add
- ✅ **Edge cases**: Empty library, empty search results, special characters in artist/title
- **Status**: ✅ COMPLETED (14 tests passing)

### Step 2.7: Playlist Management View ✅
**Files**: `Pages/Playlist.razor`, `Karamel.Web.Tests/PlaylistPageTests.cs`

#### Implementation:
- ✅ Display current queue with drag-drop reordering (HTML5 Drag API, if singers allowed to reorder)
- ✅ Show "Now Playing" (index 0) + "Up Next" sections
- ✅ Remove button for each song
- ✅ Clear playlist button (asks for confirmation)
- ✅ Listens to Broadcast Channel for real-time updates from main tab
- ✅ No folder access needed (metadata-only view)

#### Testing:
- ✅ **Unit tests**: Verify "Now Playing" displays first song in queue with correct artist, title, and singer name
- ✅ **Unit tests**: Verify "Up Next" displays remaining songs in order with correct metadata
- ✅ **Unit tests**: Test RemoveSongAction dispatch when remove button clicked
- ✅ **Unit tests**: Test ReorderPlaylistAction dispatch with correct old/new indices on drag-drop
- ✅ **Unit tests**: Test ClearPlaylistAction dispatch when clear button clicked (with confirmation dialog)
- ✅ **Unit tests**: Verify empty state message when playlist is empty
- ✅ **Unit tests**: Verify drag-drop UI only enabled when singers allowed to reorder (session setting)
- ✅ **Edge cases**: Single song in queue, queue becomes empty after removal, reorder to same position
- **Status**: ✅ COMPLETED (11 tests passing)

### Step 2.8: Singer View ✅
**Files**: `Pages/SingerView.razor`, `Karamel.Web.Tests/SingerViewTests.cs`

#### Implementation:
- ✅ If `RequireSingerName`: Show name entry form → confirm button → store in session state
- ✅ Embeds `<LibrarySearch>` component with mobile-optimized styling
- ✅ Large touch-friendly "Add to Queue" buttons
- ✅ **Hard-coded limit**: Max 10 songs per singer (tracked in PlaylistState)
- ✅ Success toast: "Song added! It's now #{position} in queue"
- ✅ Error toast if limit reached: "Maximum 10 songs per singer"
- ✅ No folder access needed (uses broadcast metadata from main tab)

#### Testing:
- ✅ **Unit tests**: Verify name entry form displays when RequireSingerName is true
- ✅ **Unit tests**: Verify name entry form is skipped when RequireSingerName is false
- ✅ **Unit tests**: Test singer name is stored in session state after confirmation
- ✅ **Unit tests**: Verify LibrarySearch component is displayed after name confirmation
- ✅ **Unit tests**: Test 10-song limit validation per singer (tracked in PlaylistState)
- ✅ **Unit tests**: Verify success toast shows with correct queue position
- ✅ **Unit tests**: Verify error toast displays when singer reaches 10-song limit
- ✅ **Unit tests**: Test mobile-optimized styling and touch-friendly buttons
- ✅ **Edge cases**: Empty singer name validation, special characters in names, session state persistence
- **Status**: ✅ COMPLETED

### Step 2.9: Next Song View ✅
**Files**: `Pages/NextSongView.razor`, `wwwroot/js/qrcode.js`, `wwwroot/js/qrcode.test.js`, `Karamel.Web.Tests/NextSongViewTests.cs`

#### Implementation: ✅
- **QR Code Library**: Import QRCode.js from CDN `https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js`
- If queue has songs:
  - Center: Display next song (Artist above Title, Singer name below in different color)
  - Lower left: "Sing a song" + QR code linking to `/singer?session={guid}`
  - Auto-advance to PlayerView after configured pause seconds
- If queue empty:
  - Center: "Sing a song" message + QR code (large) - Left side shows a large microphone.
- Listens to Broadcast Channel for playlist updates

#### Testing: ✅
- **Unit tests**: Verify display of next song from queue with correct artist, title, and singer name (18 C# tests, 20 JS tests - all passing)
- **Unit tests**: Test empty queue state shows "Sing a song" message and large QR code
- **Unit tests**: Verify QR code generation with correct session URL
- **Unit tests**: Test auto-advance timer to PlayerView after configured pause seconds
- **Unit tests**: Verify broadcast channel listener updates when playlist changes
- **Edge cases**: Queue becomes empty while timer is running, session GUID validation, pause duration variations

### Step 2.10: Player View (Refactor KaraokePlayer.razor)
**Files**: `Pages/PlayerView.razor` (renamed from KaraokePlayer.razor), `Karamel.Web.Tests/PlayerViewTests.cs`

#### Implementation:
- Full-screen CDG canvas with synchronized audio
- Loads song files from directory handle (JavaScript file access)
- Controls overlay (play/pause/stop) visible only on hover over lower center
- Auto-advances to NextSongView when song ends
- **Retains folder access** - all playback logic stays in JavaScript
- Dispatches NextSong action on completion (removes song from queue index 0)
- Allows to open embedded playlist or singer view by hovering over the left side (15 pixels) of the screen. This hovering will cause an expand section icon to show. if this is clicked, the left side will show a pane where I can switch between showing a Singer view or the Playlist view. It will take up 20% of the screen width.

#### Testing: ✅
- ✅ **Unit tests**: Verify session validation and invalid session error display
- ✅ **Unit tests**: Test CurrentSong is loaded from PlaylistState automatically
- ✅ **Unit tests**: Verify full-screen canvas display without card wrappers
- ✅ **Unit tests**: Test controls overlay appears/hides based on hover state
- ✅ **Unit tests**: Verify side panel opens on left-edge hover and icon click
- ✅ **Unit tests**: Test switching between Singer and Playlist views in side panel
- ✅ **Unit tests**: Verify NextSong action dispatch when song playback ends
- ✅ **Unit tests**: Test navigation to NextSongView after song completion
- ✅ **Unit tests**: Verify proper handling when queue is empty after song ends
- ✅ **Edge cases**: Session without CurrentSong, playback errors, side panel state persistence
- **Status**: ✅ COMPLETED (19 tests passing)

### Step 2.11: Navigation & Session Flow ✅ COMPLETED
**Files**: `Layout/NavMenu.razor`, `Layout/MainLayout.razor`, `App.razor`, `Karamel.Web.Tests/NavigationFlowTests.cs`, `Karamel.Web.Tests/SessionTestBase.cs`, `Pages/*.razor`, `STRICT_SESSION_VALIDATION_PLAN.md`

#### Implementation:
- Remove NavMenu component from MainLayout (session-based routing via URLs)
- Session routing:
  - `/` → Home.razor (session initialization)
  - `/nextsong?session={guid}` → NextSongView.razor
  - `/player?session={guid}` → PlayerView.razor (same tab as NextSong)
  - `/playlist?session={guid}` → Playlist.razor (new tab, no file access)
  - `/singer?session={guid}` → SingerView.razor (new tab or QR code access)
- **STRICT validation**: All views validate `?session={guid}` parameter is present, valid GUID, and matches CurrentSession.SessionId
- Multi-session support: Different tabs can run independent sessions simultaneously
- Test session handling: Can add song to playlist in SingerView. It is broadcast to NextSongView and PlaylistView.
- Test: first song is added to playlist and NextSongView shows it and navigates to PlayerView.

#### Testing:
- ✅ **Unit tests**: Verify MainLayout no longer includes NavMenu component
- ✅ **Unit tests**: Test minimal layout structure without sidebar navigation
- ✅ **Unit tests**: Verify all views validate session GUID from query parameters (STRICT)
- ✅ **Unit tests**: Test invalid session GUID handling (error display)
- ✅ **Unit tests**: Test missing session parameter handling
- ✅ **Edge cases**: Malformed GUIDs, empty session parameters, valid GUID but no session data
- ✅ **Created SessionTestBase**: Centralized test infrastructure with SetupTestWithSession() helper
- **Status**: ✅ COMPLETED (92 tests passing, 9 skipped with documented reasons)

### Step 2.12: PlayerView starts playback ✅ COMPLETED
- first song is added to playlist
- Currently PlayerView reports "No song is currently playing. Please select a song from the playlist.". Instead it should start playing the song.

---

## Phase 3: Styling & UX Polish ✅ COMPLETED

### Step 3.1: Design Review ✅ COMPLETED
**Deliverable**: Mockups or design direction discussion with user

- Color scheme
- Typography
- Layout preferences (sidebar vs. top nav)
- Singer view simplified vs. admin view
- Mobile responsiveness requirements
- favicon and program icon

### Step 3.2: Custom CSS ✅ COMPLETED
**Files**: `wwwroot/css/app.css`, component-specific CSS

- Implement approved design
- Consistent spacing/padding
- Button styles for primary actions
- Canvas/player styling
- Drag-drop visual feedback

### Step 3.3: Responsive Layout ✅ COMPLETED
- Test on different screen sizes
- Optimize for admin second-screen scenario
- Singer view mobile-friendly

---

## Phase 4: Licensing & Legal Review ✅ COMPLETED

### Step 4.1: Dependency License Audit ✅ COMPLETED
**Review licenses**:
- ✅ **cdgraphics** (npm): ISC License - Compatible with MIT ✓
- ✅ **jsmediatags** (npm): LGPL-3.0 - Review usage (client-side library use is generally OK)
- ✅ **QRCode.js** (npm): MIT License - Compatible ✓
- ✅ **.NET / Blazor**: MIT License - Compatible ✓
- ✅ **Fluxor**: MIT License - Compatible ✓

### Step 4.2: Attribution ✅ COMPLETED
**Files**: `Pages/About.razor` or `README.md`

- Add credits for cdgraphics library
- Add credits for jsmediatags
- Add credits for QRCode.js
- Link to original libraries
- Include required license notices

### Step 4.3: License File Update ✅ COMPLETED
**Files**: `LICENSE`

- Confirm MIT license for Karamel-Web code
- Add THIRD-PARTY-NOTICES.md for dependencies
- Document any restrictions (e.g., browser compatibility)

---

## Phase 5: Testing & Polish ✅ COMPLETED

### Step 5.1: Error Handling ✅ COMPLETED
- File access denied scenarios
- No CDG file found for MP3
- Corrupt CDG files
- Browser compatibility fallback message

### Step 5.2: Loading States ✅ COMPLETED
- ✅ "Scanning library..." indicator
- ✅ Progress for large directories (batched updates)
- ✅ Skeleton loaders for song lists

### Step 5.3: User Feedback ✅ COMPLETED
- Toast notifications for actions (implemented in Components/LibrarySearch.razor and Pages/SingerView.razor)
- Confirmation dialogs for destructive actions (Clear All in Pages/Playlist.razor uses JS confirm)
- Success messages for song additions (SingerView shows success toast with queue position)

---

## Future Phases (Backend Integration)

### Phase 6: Backend API
**Files**: `openapi-summary.md`
- ASP.NET Core Web API
- SignalR for real-time playlist sync - we are already using broadcast api in frontend. Should we move that all to the backend?
- Azure SQL for songs/sessions/users
- Authentication (Azure AD B2C or custom) - anybody having the link of the session may add songs.
- Timeout of session 30 minutes after the last NextSongView with the sessionGuid has been shown and only if no paused PlayerView with the sessionGuid is shown.

Backend API — Substeps (incremental)
Phase 6 will be implemented as a sequence of small, testable substeps. Each substep focuses on a single responsibility and has clear acceptance criteria. The overall goal: replace BroadcastChannel with a server-backed SignalR sync, persist sessions/playlists, enforce link-based session tokens, and support Azure App Service + Azure SQL deployment while keeping SQLite for local dev.

### Step 6.1 Scaffolding: backend project and health endpoints
**Status**: ✅ COMPLETED (scaffold + tests added)
### Step 6.2 Database layer: provider-agnostic EF Core + repositories
- Purpose: Define `BackendDbContext`, repository interfaces and implementations so the DB provider (SQLite dev / SQL Server prod) is pluggable.
**Status**: ✅ COMPLETED

Files added/updated for this step:

- `Karamel.Backend/Models/Session.cs`
- `Karamel.Backend/Models/Playlist.cs`
- `Karamel.Backend/Models/PlaylistItem.cs`
- `Karamel.Backend/Data/BackendDbContext.cs`
- `Karamel.Backend/Repositories/IRepository.cs`
- `Karamel.Backend/Repositories/ISessionRepository.cs`
- `Karamel.Backend/Repositories/EfRepository.cs`
- `Karamel.Backend/Repositories/SessionRepository.cs`
- `Karamel.Backend/Karamel.Backend.csproj` (EF Core package references)
- `Karamel.Backend/Program.cs` (DbContext and repository DI registration)
- `Karamel.Backend.Tests/SessionRepositoryTests.cs` (InMemory provider tests)

Notes:
- Unit tests using `Microsoft.EntityFrameworkCore.InMemory` have been added and pass locally.
- `Program.cs` configures the DB provider via `DB_PROVIDER` env var (defaults to `Sqlite`).

#### Step 6.2.1 Tests for DB layer
- Purpose: Define unit and integration tests to validate provider-agnostic behavior and repository correctness.
- Tests:
  - Unit tests using `Microsoft.EntityFrameworkCore.InMemory` to verify basic CRUD operations for `Session`, `Playlist`, and `PlaylistItem` repositories.
  - Integration tests using SQLite in-memory mode (`DataSource=:memory:`) to validate migrations and provider-specific behaviors where necessary.
  - Tests should assert that repositories work correctly when swapping providers (InMemory → SQLite) and that `BackendDbContext` can be configured via DI.
  - Place tests in `Karamel.Backend.Tests` and ensure they run with `dotnet test`.


### Step 6.3 Session + Playlist models and REST API (link-based tokens)
- Purpose: Implement `Session`, `Playlist`, `PlaylistItem` models and REST endpoints for create/get/heartbeat/end and playlist mutations. Issue link-based tokens at session creation.
- Files to add/update: `Karamel.Backend/Models/Session.cs`, `Karamel.Backend/Models/PlaylistItem.cs`, `Karamel.Backend/Controllers/SessionController.cs`, `Karamel.Backend/Controllers/PlaylistController.cs`, `Karamel.Backend/Services/TokenService.cs`
- Quick verification: Unit tests for token validation and `POST /api/sessions` producing a `linkToken`; `dotnet test` for new tests.
- Estimate: 1 day. Risk: medium.
- Acceptance: Sessions can be created and read; link token validates for protected endpoints.

### Step 6.4 Real-time sync: `PlaylistHub` SignalR implementation (server-mode default)
- Purpose: Implement `PlaylistHub` at `/hubs/playlist`, enforce link-token authorization for mutation methods, and make the hub the single source of truth for playlist state.
- Files to add/update: `Karamel.Backend/Hubs/PlaylistHub.cs`, `Karamel.Backend/Contracts/*` (DTOs), update `Karamel.Backend/Program.cs` to add SignalR and hub endpoints.
- Quick verification: Connect a test SignalR client to the hub, call `JoinSession` and receive `ReceivePlaylistUpdated` messages after mutations.
- Estimate: 1–2 days. Risk: high (ordering, race conditions).
- Acceptance: Clients connected to the hub receive canonical playlist updates after server-side mutations.

### Step 6.5 Frontend migration: remove BroadcastChannel, add SignalR bridge
- Purpose: Replace `sessionBridge.js` BroadcastChannel logic with `signalRBridge.js` and update `SessionService.cs` to use SignalR by default. Make server-mode default for the app.
- Files to update: `Karamel.Web/wwwroot/js/sessionBridge.js` (remove BC logic), `Karamel.Web/wwwroot/js/signalRBridge.js` (new), `Karamel.Web/Services/SessionService.cs` (wire server tokens and SignalR interop), `Karamel.Web/Pages/Home.razor` (session creation flow updates to show shareable link/token/QR)
- Quick verification: `dotnet build`; frontend JS tests: `cd Karamel.Web/wwwroot; npm run test:run`; manual test with backend running—open main tab and a secondary tab and verify playlist sync via SignalR.
- Estimate: 1–2 days. Risk: high (breaking previous local-only behavior). This step will remove BroadcastChannel entirely.
- Acceptance: Frontend uses SignalR for state sync; no BroadcastChannel code remains in active flow.

### Step 6.6 Session cleanup & heartbeats
- Purpose: Implement `POST /api/sessions/{id}/heartbeat` and a background cleanup job that expires sessions per the rule: session expires 30 minutes after last NextSongView activity and only if no paused PlayerView is present.
- Files to add/update: `Karamel.Backend/Services/SessionCleanupService.cs`, `Karamel.Backend/Controllers/SessionController.cs` (heartbeat), update `Karamel.Web/Pages/NextSongView.razor` and `Karamel.Web/Pages/PlayerView.razor` to call heartbeat endpoints.
- Quick verification: Integration test using short TTL to simulate expiry; manual test to confirm clients receive `ReceiveSessionEnded` and handle gracefully.
- Estimate: 1 day. Risk: medium.
- Acceptance: Sessions expire as configured and clients are notified.

### Step 6.7 Deployment prep (Azure)
- Purpose: Add `appsettings.Production.json` with Azure SQL connection placeholders, a short `DEPLOYMENT.md` describing Azure App Service steps, and ensure WebSockets are enabled in deployment guidance.
- Files to add: `Karamel.Backend/appsettings.Production.json` (template), `DEPLOYMENT.md` (short Azure checklist), `Karamel.Backend/Dockerfile` (optional, recommended for container deployments).
- Quick verification: CI step to run migrations against a staging SQL Server and deploy to App Service (manual step in short doc). 
- Estimate: 4–8 hours. Risk: low–medium.
- Acceptance: Deployment doc present and connection settings documented for Azure App Service + Azure SQL.

---

### Notes & Constraints
- Non-main tabs will not access media files; they operate on metadata only. Main tab retains File System Access API handle and handles actual file I/O for playback.
- Link tokens expire together with the containing session. Token TTL is configured through `KARAMEL_SESSION_TTL_MINUTES`.
- BroadcastChannel will be removed — accept that main-tab responsibilities remain for file access.

### Phase 6: Database Schema Considerations (adjusted for FUTURE_REQUIREMENTS)
The following notes augment the Phase 6 DB design to support future requirements in `FUTURE_REQUIREMENTS.md` (video support, admin playback controls, and richer NextSongView data). They should be incorporated into the `BackendDbContext` models and repository contracts.

Schema additions and considerations:

- `Song` table enhancements
  - Add `MediaType` enum column (e.g., `Mp3Cdg`, `Video`) to indicate handling differences.
  - Add `VideoFileName` and `VideoMetadata` JSON column (nullable) for video-specific fields (codec, container, resolution, durationMs) so PlayerView can treat videos like songs.
  - Keep `Mp3FileName`/`CdgFileName` nullable when `MediaType` is `Video`.

- `PlaylistItem` changes
  - Add `StopAfterCurrent` boolean flag at `Session` level (not per item) — see `Session` below. PlaylistItem remains metadata-only referencing `Song.Id`.
  - `PlaylistItem` must include `displayTitle` and `displayArtist` snapshot fields to preserve what was shown at the time of adding (avoid metadata drift if library metadata changes).

- `Session` table changes
  - Add `StopAfterCurrent` boolean column to reflect admin intent to stop playback at end of current song.
  - Add `NowPlayingItemId` (nullable FK to `PlaylistItem`) and `NowPlayingPositionMs` integer to represent server canonical playback position for NextSongView and cross-tab consistency.
  - Add `LastNextSongViewAt` DateTime and `AnyPausedPlayerViewCount` integer or derived state to enable TTL logic: session expires 30 minutes after `LastNextSongViewAt` only if `AnyPausedPlayerViewCount == 0`.

- `SessionActivity` / heartbeat table
  - Consider a lightweight `SessionActivity` table for audit/cleanup that records `{ SessionId, Source, IsPaused, Timestamp }` to allow accurate TTL decisions and diagnostics without frequent large row updates to `Session`.

- Indexing & queries
  - Index `Session.ExpiresAt`, `Session.LastNextSongViewAt`, and `PlaylistItem.SessionId` to make cleanup and playlist retrieval efficient.
  - Consider composite index on `PlaylistItem(SessionId, AddedAt)` for queue ordering.

- DTO & migration notes
  - Keep DTOs provider-agnostic and use repository interfaces so switching SQLite ↔ SQL Server is straightforward.
  - Migrations: generate against SQLite dev, but validate against SQL Server in staging before production deployment.



### Phase 7: Multi-Device Support
- WebSocket connections for playlist sync - see phase 6. Currently using broadcast api.
- Admin controls: volume

### Phase 8: Production Deployment
- Azure App Service hosting
- CI/CD pipeline
- Monitoring & logging
- Performance optimization

### Phase 9: Security review
- Check for possible dDOS attacks - do not allow to add more than one song in 3 seconds.
- Perform a full security review as a trained security expert, advise on options where we can improve

---
## Technical Notes

### File System Access API Limitations
- Cannot persist directory handles in localStorage (security restriction)
- User must re-grant access each session
- Only works in Chromium browsers (Chrome, Edge, Opera)
- Requires HTTPS in production (localhost OK for dev)

### CDGraphics.js Integration
- ES module import from CDN
- Instantiation: `new CDGraphics(arrayBuffer)`
- Render API: `frame = cdg.render(time)` returns `{imageData, isChanged, backgroundRGBA, contentBounds}`
- Canvas rendering: `ctx.putImageData(frame.imageData, 0, 0)`

### State Management Strategy
- Fluxor for predictable state flow (session config, library metadata, playlist)
- Actions dispatched from components
- Reducers update state immutably
- Effects for side effects (broadcast channel messaging, future API calls)
- **File I/O stays in JavaScript**: Directory handle and file playback never cross JS/C# boundary

### Blazor Hot Reload Gotchas
- C#/Razor: Hot reload works with `dotnet watch`
- JavaScript in wwwroot: Requires browser refresh
- Disable cache in DevTools for JS changes
- CSS: Usually hot reloads

---

## Open Questions (ANSWERED)

1. **Sorting**: ✅ Library defaults to alphabetical by Artist, then Title (ascending)
2. **Duplicates**: ✅ Only first song with same Artist and title will be added to library..
3. **Singer names**: ✅ App stores current singer name in session state. Name persists for all songs added by that singer until they change it. No history tracking in Phase 2.
4. **Playlist limit**: ✅ Max 10 songs per singer to prevent spam
5. **Audio controls**: ✅ Singer view shows library search only. "Now Playing" info remains on main tab (NextSongView/PlayerView) and Playlist view.

---

## Success Criteria

### Phase 1 Complete When:
- ✅ Counter.razor and Weather.razor deleted
- ✅ NavMenu.razor updated with no template links
- ✅ Clean workspace ready for feature development

### Phase 2 Complete When:
- ✅ Admin can select folder and scan 100+ song library in <5 seconds
- ✅ Library displays with accurate artist/title from ID3 tags or filename parsing
- ✅ Playlist supports add/remove/reorder operations with Broadcast Channel sync
- ✅ Singer view works on mobile with QR code access, no folder permissions needed
- ✅ NextSongView displays upcoming song with QR code for 5 seconds
- ✅ PlayerView (refactored KaraokePlayer) plays MP3+CDG with auto-advance
- ✅ Session state syncs across all tabs via Broadcast Channel API
- ✅ Main tab retains folder access throughout Home → NextSong → Player flow

### Phase 3 Complete When:
- ✅ User approves design direction and mockups
- ✅ Consistent styling across all views (colors, typography, spacing)
- ✅ Responsive layout on desktop, tablet, and mobile
- ✅ Drag-drop has clear visual feedback
- ✅ Touch-friendly buttons on singer view

### Phase 4 Complete When:
- ✅ All licenses verified compatible (cdgraphics, jsmediatags, QRCode.js, Fluxor)
- ✅ Attribution added to About page or README
- ✅ THIRD-PARTY-NOTICES.md created with all dependency licenses

---

**Last Updated**: December 28, 2025  
**Current Phase**: Phase 1 (directly after prototype)  
**Next Milestone**: Remove Template Files
