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

### Step 2.9: Next Song View
**Files**: `Pages/NextSongView.razor`, `wwwroot/js/qrcode.js`

- **QR Code Library**: Import QRCode.js from CDN `https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js`
- If queue has songs:
  - Center: Display next song (Artist above Title, Singer name below in different color)
  - Lower left: "Sing a song" + QR code linking to `/singer?session={guid}`
  - Auto-advance to PlayerView after configured pause seconds
- If queue empty:
  - Center: "Sing a song" message + QR code (large)
- Listens to Broadcast Channel for playlist updates

### Step 2.10: Player View (Refactor KaraokePlayer.razor)
**Files**: `Pages/PlayerView.razor` (renamed from KaraokePlayer.razor)

- Full-screen CDG canvas with synchronized audio
- Loads song files from directory handle (JavaScript file access)
- Controls overlay (play/pause/stop) visible only on hover over lower center
- Auto-advances to NextSongView when song ends
- **Retains folder access** - all playback logic stays in JavaScript
- Dispatches NextSong action on completion (removes song from queue index 0)
- allows to open embedded playlist or singer view by hovering over the left side (15 pixels) of the screen. This hovering will cause an expand section icon to show. if this is clicked, the left side will show a pane where I can switch between showing a Singer view or the Playlist view. It will take up 20% of the screen width.

### Step 2.11: Navigation & Session Flow
**Files**: `Layout/NavMenu.razor`, `App.razor`

- Remove NavMenu component from MainLayout (session-based routing via URLs)
- Session routing:
  - `/` → Home.razor (session initialization)
  - `/nextsong?session={guid}` → NextSongView.razor
  - `/player?session={guid}` → PlayerView.razor (same tab as NextSong)
  - `/playlist?session={guid}` → Playlist.razor (new tab, no file access)
  - `/singer?session={guid}` → SingerView.razor (new tab or QR code access)
- All views except Home validate session GUID and load state from Fluxor

---

## Phase 3: Styling & UX Polish

### Step 3.1: Design Review
**Deliverable**: Mockups or design direction discussion with user

- Color scheme
- Typography
- Layout preferences (sidebar vs. top nav)
- Singer view simplified vs. admin view
- Mobile responsiveness requirements

### Step 3.2: Custom CSS
**Files**: `wwwroot/css/app.css`, component-specific CSS

- Implement approved design
- Consistent spacing/padding
- Button styles for primary actions
- Canvas/player styling
- Drag-drop visual feedback

### Step 3.3: Responsive Layout
- Test on different screen sizes
- Optimize for admin second-screen scenario
- Singer view mobile-friendly

---

## Phase 4: Licensing & Legal Review

### Step 4.1: Dependency License Audit
**Review licenses**:
- ✅ **cdgraphics** (npm): ISC License - Compatible with MIT ✓
- ✅ **jsmediatags** (npm): LGPL-3.0 - Review usage (client-side library use is generally OK)
- ✅ **QRCode.js** (npm): MIT License - Compatible ✓
- ✅ **.NET / Blazor**: MIT License - Compatible ✓
- ✅ **Fluxor**: MIT License - Compatible ✓

### Step 4.2: Attribution
**Files**: `Pages/About.razor` or `README.md`

- Add credits for cdgraphics library
- Add credits for jsmediatags
- Add credits for QRCode.js
- Link to original libraries
- Include required license notices

### Step 4.3: License File Update
**Files**: `LICENSE`

- Confirm MIT license for Karamel-Web code
- Add THIRD-PARTY-NOTICES.md for dependencies
- Document any restrictions (e.g., browser compatibility)

---

## Phase 5: Testing & Polish

### Step 5.1: Error Handling
- File access denied scenarios
- No CDG file found for MP3
- Corrupt CDG files
- Browser compatibility fallback message

### Step 5.2: Loading States
- "Scanning library..." indicator
- Progress for large directories
- Skeleton loaders for song lists

### Step 5.3: User Feedback
- Toast notifications for actions
- Confirmation dialogs for destructive actions
- Success messages for song additions

---

## Future Phases (Backend Integration)

### Phase 6: Backend API
- ASP.NET Core Web API
- SignalR for real-time playlist sync
- Azure SQL for songs/sessions/users
- Authentication (Azure AD B2C or custom)

### Phase 7: Multi-Device Support
- QR code generation for session sharing
- WebSocket connections for playlist sync
- Admin controls: skip, pause, volume

### Phase 8: Production Deployment
- Azure App Service hosting
- CI/CD pipeline
- Monitoring & logging
- Performance optimization

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
