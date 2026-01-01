# Karamel.Backend OpenAPI & SignalR Contract Summary

## Overview
This document summarizes the OpenAPI REST surface and the SignalR `PlaylistHub` contract for Phase 6. The backend persists session and playlist state, issues link-based session tokens, and provides real-time synchronization via SignalR. Tokens expire together with their sessions. The backend uses EF Core with pluggable providers (SQLite for dev, SQL Server for production).

Base path: `/api`
SignalR hub route: `/hubs/playlist`

---

## REST API Endpoints (summary)

### Session
- POST `/api/sessions`
  - Purpose: Create a new session and return a link token.
  - Request body (JSON):
    - `createdBy` (string, optional)
    - `settings` (object, optional)
    - `ttlMinutes` (integer, optional, default 30)
  - Response (201):
    ```json
    {
      "sessionId": "GUID",
      "linkToken": "string",
      "expiresAt": "2025-12-31T12:34:56Z"
    }
    ```

- GET `/api/sessions/{sessionId}`
  - Purpose: Retrieve session metadata and current playlist snapshot.
  - Response (200): `Session` DTO (see Models section).

- POST `/api/sessions/{sessionId}/heartbeat`
  - Purpose: Mark activity for TTL renewal and paused state.
  - Request body: `{ "source": "NextSongView" | "PlayerView", "isPaused": true|false }`
  - Response (204)

- POST `/api/sessions/{sessionId}/end`
  - Purpose: Explicitly end a session (invalidate token, notify clients).
  - Response (204)

---

## SignalR Hub: `PlaylistHub` (route `/hubs/playlist`)

### Client -> Server invokable methods
- `Task JoinSession(string sessionId, string linkToken)`
  - Validates token and adds the connection to the session group.

- `Task LeaveSession(string sessionId)`
  - Removes the connection from the session group.

- `Task AddItem(string sessionId, PlaylistItemDto item)`
  - Adds a new item to the session's playlist (requires token).

- `Task RemoveItem(string sessionId, Guid itemId)`
  - Removes an item (requires token).

- `Task Reorder(string sessionId, string[] orderedItemIds)`
  - Reorders playlist (requires token).

- `Task UpdateNowPlaying(string sessionId, NowPlayingDto nowPlaying)`
  - Updates now-playing info (requires token).

### Server -> Client callbacks
- `ReceivePlaylistUpdated(PlaylistDto playlist)`
  - Full snapshot broadcast after mutations.

- `ReceivePlaylistDelta(PlaylistDeltaDto delta)`
  - Optional delta for lightweight updates (add/remove/reorder).

- `ReceiveNowPlaying(NowPlayingDto nowPlaying)`
  - Now playing update broadcast.

- `ReceiveSessionEnded(string sessionId)`
  - Notify clients the session has ended.

- `ReceiveError(string code, string message)`
  - Generic error callback for validation/auth errors.

### Hub Behavioral Rules
- Server sequences mutations and broadcasts canonical state to avoid client-side divergence.
- Mutations must be idempotent where possible (clients may supply `itemId`; server returns canonical `itemId`).
- Hub enforces link-token validation on join and mutating calls.

---

## Models (sketch)

### Session
- `sessionId` (GUID)
- `createdAt` (DateTime)
- `expiresAt` (DateTime)
- `createdBy` (string, optional)
- `settings` (JSON object)

### PlaylistItem
- `itemId` (GUID)
- `songId` (GUID) - reference to library song
- `title` (string)
- `artist` (string)
- `durationMs` (int)
- `mp3FileName` (string)
- `cdgFileName` (string)
- `metadata` (JSON)
- `addedBy` (string)
- `addedAt` (DateTime)

### Playlist
- `sessionId` (GUID)
- `items` (ordered array of `PlaylistItem`)
- `nowPlaying` (nullable) `{ itemId, positionMs }`

---

## Auth & Tokens
- Link tokens are generated at session creation and are valid until session expiry.
- Tokens must be presented as `Authorization: Bearer <linkToken>` for REST mutation endpoints.
- For SignalR connections, token can be passed in query string for convenience but must be validated server-side.
- Tokens are invalidated when session ends.

---

## Database notes (EF Core)
- Use `BackendDbContext` with provider-agnostic model definitions.
- Dev provider: SQLite (local file) — use in `appsettings.Development.json`.
- Prod provider: SQL Server (Azure SQL) — use in `appsettings.Production.json` / Azure App Settings.
- Keep migrations provider-agnostic where possible; generate migrations in dev (SQLite) and apply to prod SQL Server as part of deployment.
- Recommended pattern: define repository interfaces (e.g., `ISessionRepository`) and implement EF-backed repository. This isolates DB provider details and simplifies tests (in-memory providers available).

---

## Short Azure Deployment Checklist
- Create Azure SQL database and server; note connection string.
- Create App Service and enable WebSockets.
- Configure App Settings:
  - `ConnectionStrings__DefaultConnection` = Azure SQL connection string
  - `ASPNETCORE_ENVIRONMENT` = `Production`
  - `KARAMEL_SESSION_TTL_MINUTES` = `30` (or configured value)
- Run EF migrations during deployment or as a pipeline step.
- Ensure CORS allows the Blazor frontend origin and that WebSocket traffic is permitted.

---

(End of design summary)
