# Karamel.Backend OpenAPI & SignalR Contract Summary

## Overview
This document summarizes the OpenAPI REST surface and the SignalR `PlaylistHub` contract. The backend persists session, playlist, and library state with dual token authentication (admin/singer), and provides real-time synchronization via SignalR. The backend uses EF Core with pluggable providers (SQLite for dev, SQL Server for production).

Base path: `/api`
SignalR hub route: `/hubs/playlist`

---

## REST API Endpoints

### Sessions (`/api/sessions`)

#### POST `/api/sessions`
Create a new session and return dual tokens (admin + singer).

**Request body:**
```json
{
  "requireSingerName": false,
  "pauseBetweenSongsSeconds": 5,
  "allowSingersToReorder": false
}
```

**Response (201):**
```json
{
  "id": "GUID",
  "adminToken": "string",
  "singerToken": "string",
  "linkToken": "string",  // Deprecated - same as adminToken
  "requireSingerName": false,
  "pauseBetweenSongsSeconds": 5,
  "allowSingersToReorder": false
}
```

#### GET `/api/sessions/{id}`
Retrieve session configuration (WITHOUT tokens).

**Response (200):**
```json
{
  "id": "GUID",
  "requireSingerName": false,
  "pauseBetweenSongsSeconds": 5,
  "allowSingersToReorder": false
}
```

#### POST `/api/sessions/{id}/heartbeat`
Extend session expiry time.

**Request body:**
```json
{
  "extendMinutes": 30
}
```

**Response (200):** OK

#### POST `/api/sessions/{id}/end`
End a session immediately.

**Request body:**
```json
{
  "force": true
}
```

**Response (200):** OK

---

### Library (`/api/sessions/{sessionId}/library`)

**Authentication:** All library endpoints require `X-Link-Token` header.

#### POST `/api/sessions/{sessionId}/library/bulk`
Bulk upload sanitized song metadata (max 5000 songs per request).

**Request body:**
```json
[
  {
    "id": "GUID",
    "artist": "string",
    "title": "string",
    "metadataJson": "string (optional JSON string)"
  }
]
```

**Response (202):** Accepted

#### GET `/api/sessions/{sessionId}/library`
Get paginated library for session.

**Query parameters:**
- `page` (default: 1)
- `pageSize` (default: 50)
- `search` (optional filter)
- `sort` (optional: "artist", "title")

**Response (200):**
```json
[
  {
    "id": "GUID",
    "sessionId": "GUID",
    "artist": "string",
    "title": "string",
    "metadataJson": "string",
    "addedAt": "2026-02-07T12:34:56Z"
  }
]
```

**Response headers:**
- `X-Total-Count`: Total number of songs in library

#### GET `/api/sessions/{sessionId}/library/{songId}`
Get a single song by ID.

**Response (200):** Single `SongListItemDto` object (same structure as array item above)

---

## SignalR Hub: `PlaylistHub` (route `/hubs/playlist`)

**Authentication:** Mutation methods require `X-Link-Token` header (validated by `LinkTokenHubFilter`).  
Token can be passed via HTTP header or query string (`?access_token=...`).

### Client -> Server Invokable Methods

#### Public Methods (No Auth Required)
- `Task JoinSession(string sessionId)`
  - Adds connection to session group for receiving updates

- `Task LeaveSession(string sessionId)`
  - Removes connection from session group

- `Task<object> GetLibraryPage(Guid sessionId, int page, int pageSize, string? search, string? sort)`
  - Returns: `{ items: SongListItemDto[], page, pageSize, totalCount }`

- `Task<IEnumerable<object>> SearchLibrary(Guid sessionId, string query, int maxResults)`
  - Returns up to `maxResults` songs matching query

#### Playlist Mutation Methods (Auth Required)

**Basic Operations:**
- `Task AddItemAsync(Guid sessionId, Guid songId, string? singerName)`
  - Adds song to playlist with optional singer name
  
- `Task RemoveItemAsync(Guid sessionId, Guid itemId)`
  - Removes item from playlist

- `Task ReorderAsync(Guid sessionId, int from, int to)`
  - Reorders items (Queued/UpNext only, excludes NowPlaying/Completed)

**Status Management:**
- `Task SetSongStatusAsync(Guid sessionId, Guid itemId, int status)`
  - Sets item status (0=Queued, 1=UpNext, 2=NowPlaying, 3=Completed)

- `Task CompleteCurrentSongAsync(Guid sessionId)`
  - Marks NowPlaying item as Completed (does NOT advance)

- `Task AdvanceToNextSongAsync(Guid sessionId)`
  - Completes current song and promotes next Queued/UpNext to NowPlaying
  - Respects `PlaybackMode` (stops if mode is `StopAfterCurrent`)

**Playback Control:**
- `Task SetStopAfterCurrentAsync(Guid sessionId)`
  - Sets `PlaybackMode` to `StopAfterCurrent` (stops after current song finishes)

- `Task ProceedPlaybackAsync(Guid sessionId)`
  - Resumes from `Stopped` state, advances to next song, sets mode to `Normal`

- `Task ClearQueueAsync(Guid sessionId)`
  - Removes all Queued and UpNext items (keeps NowPlaying and Completed)

### Server -> Client Callbacks

- `ReceivePlaylistUpdated(PlaylistUpdatedDto playlist)`
  - Broadcast after any playlist mutation
  - Contains active items (Queued + UpNext), current song, and playback mode

### Hub Behavioral Rules
- Server sequences mutations with per-session semaphores to prevent race conditions
- Auto-promotion: First Queued item auto-promotes to UpNext when no UpNext exists
- Completed items are filtered from broadcast (not sent to clients)
- All mutations broadcast full `PlaylistUpdatedDto` to session group

---

## Data Models & DTOs

### Session
```csharp
class Session {
    Guid Id
    string AdminToken      // Full permissions
    string SingerToken     // Limited permissions (future use)
    string LinkToken       // Deprecated - equals AdminToken
    DateTime CreatedAt
    DateTime? ExpiresAt
    SessionConfig Config
}
```

### SessionConfig
```csharp
class SessionConfig {
    bool RequireSingerName = false
    int PauseBetweenSongsSeconds = 5
    bool AllowSingersToReorder = false
    PlaybackMode PlaybackMode = Normal
}
```

### PlaybackMode (enum)
```csharp
enum PlaybackMode {
    Normal = 0,           // Songs advance automatically
    StopAfterCurrent = 1, // Stop after current song finishes
    Stopped = 2           // Playback stopped (no current song)
}
```

### SongStatus (enum)
```csharp
enum SongStatus {
    Queued = 0,      // In queue
    UpNext = 1,      // Next to play (auto-promoted from Queued)
    NowPlaying = 2,  // Currently playing
    Completed = 3    // Finished (filtered from client broadcasts)
}
```

### PlaylistItem
```csharp
class PlaylistItem {
    Guid Id
    Guid PlaylistId
    int Position
    string Artist
    string Title
    string? SingerName
    Guid? SongId           // FK to Songs table
    SongStatus Status
    DateTime? CompletedAt
}
```

### DTOs

**SongUploadDto** (Library bulk upload):
```csharp
record SongUploadDto(Guid Id, string Artist, string Title, string? MetadataJson)
```

**SongListItemDto** (Library GET response):
```csharp
record SongListItemDto(Guid Id, Guid SessionId, string Artist, string Title, string? MetadataJson, DateTime AddedAt)
```

**PlaylistItemDto** (SignalR broadcast):
```csharp
record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position, Guid? SongId, int Status)
```

**PlaylistUpdatedDto** (SignalR broadcast):
```csharp
record PlaylistUpdatedDto(
    Guid PlaylistId,
    Guid SessionId,
    List<PlaylistItemDto> Items,        // Active items (Queued + UpNext)
    PlaylistItemDto? CurrentSong,       // NowPlaying item or null
    int PlaybackMode                    // Current playback mode
)
```

---

## Authentication & Authorization

### Dual Token System
- **Admin Token**: Full permissions (session creation returns this as both `adminToken` and deprecated `linkToken`)
- **Singer Token**: Limited permissions (future use for public singer-only endpoints)

### Token Validation
- REST endpoints: Use `[Filters.LinkToken]` attribute on controllers/actions
- SignalR: Use `LinkTokenHubFilter` on mutation methods
- Token passed via:
  - HTTP header: `X-Link-Token: <token>`
  - SignalR query string: `?access_token=<token>`
  - SignalR HTTP header (WebSocket upgrade): `X-Link-Token: <token>`

### Token Storage
Tokens are stored in `Session` table and validated using HMAC-SHA256 signature via `ITokenService`.

### Session Expiry
- Sessions have optional `ExpiresAt` timestamp
- Extend via `/api/sessions/{id}/heartbeat` endpoint
- Background cleanup service removes expired sessions (not yet implemented)

---

## Database (EF Core)

### Provider Configuration
- **Development**: SQLite (file-based, default `karamel.db`)
- **Production**: SQL Server (Azure SQL with optional AAD Managed Identity)

**Environment variables:**
- `DB_PROVIDER`: `"Sqlite"` or `"SqlServer"` (default: `"Sqlite"`)
- `DB_USE_AAD`: `"true"` to use Managed Identity authentication (SQL Server only)
- `ConnectionStrings__DefaultConnection`: Connection string

### Tables
- **Sessions**: Session metadata and configuration (JSON column for `SessionConfig`)
- **Playlists**: One playlist per session (`Id = SessionId`)
- **PlaylistItems**: Queue items with status and position
- **Songs**: Session-scoped song library (sanitized metadata only)

### Migrations
- Generate migrations with SQLite provider during development
- Apply to production SQL Server using provider-agnostic syntax
- Keep field names and types compatible across providers

### Repository Interfaces
- `ISessionRepository`: Session CRUD operations
- `IPlaylistRepository`: Playlist management (auto-creates playlist when accessed)
- `ISongRepository`: Library management (bulk upsert, pagination, search)

---

## Azure Deployment Notes

### App Service Configuration
1. **Create Azure SQL Database** and note connection string
2. **Create App Service** with these settings:
   - Enable WebSockets (required for SignalR)
   - Runtime: .NET 10
   - Always On: Enabled (production)

3. **Configure App Settings:**
   ```
   DB_PROVIDER=SqlServer
   DB_USE_AAD=true  # Optional - for Managed Identity
   ConnectionStrings__DefaultConnection=<Azure SQL connection string>
   ASPNETCORE_ENVIRONMENT=Production
   ```

4. **Run Migrations:**
   - During deployment or as separate pipeline step
   - Use `dotnet ef database update` or migration scripts

5. **CORS Configuration:**
   - Allow Blazor WebAssembly frontend origin
   - Allow credentials (for SignalR)

### Connection String Formats

**SQL Server with username/password:**
```
Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;User ID=<username>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**SQL Server with Managed Identity (DB_USE_AAD=true):**
```
Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**SQLite (development):**
```
Data Source=karamel.db
```

---

## Architecture Summary

### One Session = One Playlist
- Each session has exactly one playlist (enforced at repository level)
- `PlaylistId` equals `SessionId`
- `PlaylistRepository.GetBySessionIdAsync()` auto-creates playlist if it doesn't exist
- Simplifies multi-tab synchronization for local single-user scenarios

### Status Flow
1. **Queued** → Item added to playlist
2. **UpNext** → Auto-promoted when queue needs next song (first Queued item)
3. **NowPlaying** → Advanced from UpNext via `AdvanceToNextSongAsync()`
4. **Completed** → Marked when song finishes (filtered from client broadcasts)

### Playback Mode Flow
1. **Normal** → Songs advance automatically (default)
2. **StopAfterCurrent** → Admin requests stop after current song
3. **Stopped** → Current song completed while in `StopAfterCurrent` mode
4. **Normal** → Admin resumes playback via `ProceedPlaybackAsync()`

### Privacy Model
- Library songs contain NO file paths (sanitized at upload)
- File access is client-side only (File System Access API)
- Backend stores metadata: Artist, Title, MetadataJson (duration, genre, etc.)

---

*Last updated: February 7, 2026*
