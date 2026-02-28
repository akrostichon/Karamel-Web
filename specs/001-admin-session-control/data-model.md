# Data Model for Admin Session Control

## SessionConfig (existing entity extended)

Represents runtime configuration options associated with a session. Stored in Sessions table.

| Field | Type | Description |
|------|------|-------------|
| SingerNameRequired | bool | Determines if singer name is mandatory when adding a song. Default false. |
| AllowSingersToReorder | bool | Whether singers are permitted to reorder the playlist. Hosts may always reorder regardless of this flag. Default true. |
| PauseBetweenSongs | int | Number of seconds to wait automatically between songs (0 = no pause). Must be ≥0 and clamped to 5–90 when entering via UI. |
| Theme | string | Display style identifier (e.g. "light", "dark"). Stored as string to allow future expansions. |

### Validation Rules

- `PauseBetweenSongs` must be a non-negative integer. UI clamps entry between 5 and 90; backend enforces range and rejects negatives with 400.
- `Theme` must be one of known values; unspecified defaults to existing session value.

## SessionState (frontend adaptation)

Previously contained `CurrentSession` (SessionDto) and other ephemeral state. Add:

- `bool IsPaused` – transient flag set via Fluxor action when pause/resume commands received.

Alternatively a separate `SessionControlState` slice could house `IsPaused` and recent config; for simplicity extend `SessionState`.

### State Transitions

- `PauseSessionAction` → sets `IsPaused = true`.
- `ResumeSessionAction` → sets `IsPaused = false`.
- `SessionConfigUpdatedAction` → updates `CurrentSession.Config` fields with new values.

## SignalR Events

- `ReceiveSessionPaused()` – no payload; triggers `PauseSessionAction` on client.
- `ReceiveSessionResumed()` – no payload; triggers `ResumeSessionAction`.
- `ReceiveConfigUpdated(SessionConfigDto config)` – payload includes updated fields; triggers `SessionConfigUpdatedAction`.

The server-side hub will expose methods:
- `PauseSessionAsync(Guid sessionId)` – validates caller is admin, broadcasts pause event.
- `ResumeSessionAsync(Guid sessionId)` – similar for resume.
- `AdvanceToNextSongAsync(Guid sessionId)` – already exists; admin clients reuse it.
- `UpdateSessionConfigAsync(Guid sessionId, SessionConfigDto config)` – admin toggles values; updates DB and broadcasts.

## Backend DTOs

- `SessionConfigDto` in `Contracts/` will mirror the above fields with `[JsonPropertyName]` attributes ensuring camelCase serialization. Conversion helpers will map between entity and DTO.

## Relationships

- Session (entity) has one config object (embedded columns). No new tables needed.

## Other Entities

No additional persistent entities; `Paused` flag is only in frontend state.


