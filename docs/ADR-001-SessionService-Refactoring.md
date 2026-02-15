# ADR-001: Refactor SessionService into Focused Services

- **Status**: Accepted
- **Date**: 2026-02-15
- **Owners**: Karamel-Web team

## Context

`SessionService` had grown into a large, multi-responsibility class handling:

- JavaScript interop and sessionStorage access
- backend API calls
- SignalR playlist operations
- connection lifecycle state
- playlist/session state restoration parsing
- playlist song enrichment with local file-path data

This created a "god object" and a circular dependency pattern where services indirectly drove Fluxor state mutation:

- `PlaylistEffects` -> `ISessionService` -> dispatching actions

The result was unclear boundaries, hard-to-maintain code, and high mock complexity in tests.

## Decision

Split responsibilities into six focused services and keep `SessionService` only as a temporary, obsolete compatibility facade.

### New service boundaries

- `ISessionStorageService`: sessionStorage + URL/session ID helpers
- `ISessionApiClient`: backend `/api/sessions` and library HTTP calls
- `ISignalRPlaylistBridge`: playlist mutations + broadcast fallback wrapper
- `ISignalRConnectionManager`: SignalR initialization/lifecycle + `IsMainTab`
- `ISongEnrichmentService`: main-tab-only enrichment of songs with local playback fields
- `IPlaylistStateSynchronizer`: state restoration and broadcast message parsing

### Architectural rule

Services are stateless helpers. Fluxor Effects orchestrate workflows and dispatch actions.

### Compatibility strategy

`ISessionService` / `SessionService` remains available as an obsolete facade that delegates to focused services. This enables an incremental migration path for components and effects.

## Theme synchronization stance

Theme handling remains intentionally split across existing layers:

- client source of truth: `localStorage` via `themeToggle.js`
- same-device propagation: BroadcastChannel update path
- cross-device/session restoration: backend session config (`SessionConfigDto.Theme`)

Refactoring does not move theme ownership to C# services; services only transport theme in relevant payloads.

## Consequences

### Positive

- Removes god object anti-pattern and clarifies service responsibilities
- Breaks circular dependency between Effects and service-driven state mutation
- Reduces test setup complexity by isolating dependencies
- Improves maintainability and allows targeted changes per responsibility

### Trade-offs

- Temporary facade adds a short-term indirection layer
- During migration, both new service interfaces and obsolete facade coexist

## Migration timeline

1. Extract and register the six new services.
2. Convert components/effects to direct, focused-service dependencies.
3. Keep `SessionService` as obsolete delegator during transition.
4. Remove obsolete facade after all call sites have migrated and compatibility period ends.

## Validation summary

- SessionService anti-pattern marked as resolved in project planning documentation.
- Architecture instructions updated to reflect new boundaries and orchestration rule.
- This ADR records the final decision and migration approach.
