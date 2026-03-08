# Feature Specification: Remove Deprecated Methods and Properties

**Feature Branch**: `003-remove-deprecated-code`  
**Created**: 2026-03-07  
**Status**: Draft  
**Input**: User description: "remove deprecated methods and properties"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Remove BroadcastPlaylistUpdatedAsync No-Op Method (Priority: P1)

The `BroadcastPlaylistUpdatedAsync()` method in `ISignalRPlaylistBridge` and `SignalRPlaylistBridge` is deprecated and does nothing (returns `Task.CompletedTask`). SignalR now handles playlist synchronization automatically through hub methods. This no-op method should be removed along with all call sites.

**Why this priority**: This is the simplest cleanup with minimal risk. The method literally does nothing and exists only as a placeholder. Removing it reduces code confusion and eliminates dead code paths.

**Independent Test**: After removal, verify all tests pass and no compilation errors occur. No functional behavior changes are expected since the method is a no-op.

**Acceptance Scenarios**:

1. **Given** `BroadcastPlaylistUpdatedAsync()` is removed from interfaces and implementations, **When** the solution is built, **Then** no compilation errors occur
2. **Given** all call sites are removed or replaced, **When** tests are run, **Then** all existing tests pass with no failures
3. **Given** a user adds a song to the playlist, **When** the playlist updates, **Then** synchronization still works correctly via SignalR hub methods

---

### User Story 2 - Remove LinkToken Backend Property and Methods (Priority: P2)

The `LinkToken` property on the `Session` model (backend) is deprecated in favor of the more explicit `AdminToken` and `SingerToken` properties. Several related methods exist solely to support `LinkToken`:
- `Session.LinkToken` property (database column)
- `SessionController` response field `linkToken` (duplicate of `adminToken`)
- `ISessionRepository.GetByLinkTokenAsync()` method
- `ITokenService.GenerateLinkToken()` and `ValidateLinkToken()` methods
- `LinkTokenHubFilter` (replaced by token-based auth using AdminToken/SingerToken)

**Why this priority**: This cleanup requires database migration to remove the column, plus updates to token validation logic. It's more invasive than P1 but still doesn't affect user-facing functionality since LinkToken is already just an alias for AdminToken.

**Independent Test**: Create a session via the API, verify it returns `adminToken` and `singerToken` but NOT `linkToken`. Verify SignalR connection still authenticates correctly using the admin/singer tokens.

**Acceptance Scenarios**:

1. **Given** the `LinkToken` database column is removed, **When** a new session is created, **Then** only `AdminToken` and `SingerToken` are stored
2. **Given** the `/api/sessions` POST endpoint is called, **When** the response is returned, **Then** it contains `adminToken` and `singerToken` but NOT `linkToken`
3. **Given** a client connects to the PlaylistHub with an `adminToken`, **When** calling mutation methods, **Then** authorization succeeds using the adminToken
4. **Given** `GetByLinkTokenAsync` is removed, **When** tests run, **Then** no test references this method
5. **Given** `LinkTokenHubFilter` is removed, **When** SignalR authorization is checked, **Then** admin/singer token validation still works correctly

---

### User Story 3 - Remove LinkToken Frontend Parameters (Priority: P3)

Several frontend service methods accept optional `linkToken` parameters that are no longer used:
- `ISignalR ConnectionManager.InitializeAsync(... string? linkToken)`
- `ISessionApiClient.UploadLibraryToServerAsync(... string? linkToken)`
- `ISessionStorageService.GenerateSessionUrlAsync(... string? linkToken)`

These parameters should be removed along with any code that passes values to them.

**Why this priority**: This is the lowest risk cleanup—these parameters are optional and likely already null in most call sites. However, it requires checking every call site to ensure no code depends on them.

**Independent Test**: Upload a library to the backend, verify it succeeds without passing a linkToken parameter. Generate a session QR code URL, verify it only contains `session` and `token` query parameters (not `linkToken`).

**Acceptance Scenarios**:

1. **Given** `linkToken` parameters are removed from service interfaces, **When** upload library is called, **Then** it succeeds without passing linkToken
2. **Given** session URLs no longer include `linkToken`, **When** a QR code is generated, **Then** it contains only `?session={guid}&token={admintoken}`
3. **Given** SignalR initialization no longer accepts linkToken, **When** connecting to the hub, **Then** authentication works using the stored adminToken
4. **Given** Home.razor backward compatibility code for `linkToken` is removed, **When** loading a session, **Then** only `adminToken` and `singerToken` are read

---

### Edge Cases

- What happens if an old client tries to connect with a `linkToken`? → The backend should reject unknown parameters gracefully (no breaking change since linkToken is already unused)
- What if the database migration fails? → Use a rollback-safe migration with backup verification before deploying
- What if existing sessions in the database have `LinkToken` but not `AdminToken`? → Migration must copy `LinkToken` value to `AdminToken` before dropping the column

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST remove `BroadcastPlaylistUpdatedAsync()` method from `ISignalRPlaylistBridge` interface and `SignalRPlaylistBridge` implementation
- **FR-002**: System MUST remove all call sites to `BroadcastPlaylistUpdatedAsync()` (primarily in `PlaylistEffects.cs`)
- **FR-003**: System MUST remove the `LinkToken` property from the `Session` model (backend)
- **FR-004**: System MUST create a database migration to drop the `LinkToken` column from the `Sessions` table
- **FR-005**: System MUST copy existing `LinkToken` values to `AdminToken` before dropping the column (data migration in the EF migration script)
- **FR-006**: System MUST remove the `linkToken` field from the `SessionController.Create()` response
- **FR-007**: System MUST remove `GetByLinkTokenAsync()` method from `ISessionRepository` and `SessionRepository`
- **FR-008**: System MUST remove `GenerateLinkToken()` and `ValidateLinkToken()` methods from `ITokenService` and `TokenService`
- **FR-009**: System MUST remove `LinkTokenHubFilter` class and its registration in `Program.cs`
- **FR-010**: System MUST remove optional `linkToken` parameters from frontend service methods: `InitializeAsync`, `UploadLibraryToServerAsync`, `GenerateSessionUrlAsync`
- **FR-011**: System MUST remove backward-compatibility code in `Home.razor` that reads `linkToken` from session JSON
- **FR-012**: System MUST update all XML documentation comments that mention "LinkToken" to use "AdminToken" instead
- **FR-013**: System MUST update all log messages that reference "LinkToken" to use "AdminToken" instead
- **FR-014**: All existing tests MUST pass after removal (zero functional regressions)

### Key Entities

- **Session** (backend model): Remove `LinkToken` property
- **ISessionRepository / SessionRepository**: Remove `GetByLinkTokenAsync()` method
- **ITokenService / TokenService**: Remove `GenerateLinkToken()` and `ValidateLinkToken()` methods
- **LinkTokenHubFilter**: Remove entire class
- **ISignalRPlaylistBridge / SignalRPlaylistBridge**: Remove `BroadcastPlaylistUpdatedAsync()` method

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero compilation errors or warnings after all deprecations are removed
- **SC-002**: All C# frontend tests pass (minimum 251 passing, 9 skipped as baseline)
- **SC-003**: All C# backend tests pass (zero failures)
- **SC-004**: All JavaScript tests pass (minimum 222 passing as baseline)
- **SC-005**: Database migration successfully removes `LinkToken` column in both SQL Server and SQLite schemas
- **SC-006**: `dotnet ef migrations list` shows the new removal migration as unapplied/applied correctly
- **SC-007**: Session creation API response contains `adminToken` and `singerToken` but NOT `linkToken`
- **SC-008**: Code search for "LinkToken" or "linkToken" returns zero results in application code (excluding third-party libraries)
- **SC-009**: No "DEPRECATED" comments remain in the codebase after cleanup
- **SC-010**: QR code URLs contain `?session={guid}&token={token}` format only (no `linkToken` query parameter)

## Constitution Review Gates *(mandatory)*

> Review these gates during spec authoring. Any ❌ must be justified before the spec is approved.

### Multi-Device & Multi-Session (Principle I)

- [x] **Remote-device safe**: Removing LinkToken does not affect remote device access—`adminToken` and `singerToken` already handle multi-device auth
- [x] **Backend as source of truth**: No changes to how backend serves as source of truth for sessions
- [x] **Session ID from backend**: No changes to session ID generation
- [x] **Session parameter validated**: No new pages introduced by this cleanup

### Privacy & GDPR (Principle II)

- [x] **No file paths transmitted**: This cleanup does not involve file paths
- [x] **Minimal data**: Removing deprecated LinkToken reduces stored data (one less column)
- [x] **Consent-gated telemetry**: No new telemetry introduced
- [x] **No sensitive logging**: Log messages updated to use AdminToken instead of LinkToken (no new PII logged)

## Assumptions

- **LinkToken values**: All existing sessions in production databases have `LinkToken == AdminToken` (or `LinkToken` is empty/null for new sessions created after adminToken/singerToken split)
- **No external dependencies**: No external systems or APIs depend on the `LinkToken` field in session responses
- **Migration can run offline**: The database migration can be run during a maintenance window or with zero-downtime deployment strategy (add column then remove in separate migration if needed)
- **Backward compatibility not required**: Since LinkToken was already deprecated and is functionally identical to AdminToken, no backward compatibility period is needed for existing clients
