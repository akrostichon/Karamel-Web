# Quickstart: Remove Deprecated Code

**Date**: 2026-03-07  
**Feature**: Remove deprecated methods and properties (LinkToken, BroadcastPlaylistUpdatedAsync)  
**Status**: Ready for implementation

## Overview

This quickstart guides you through removing deprecated code in three priority tiers:
1. **P1 (Simplest)**: Remove `BroadcastPlaylistUpdatedAsync()` no-op method
2. **P2 (Database)**: Remove `LinkToken` backend property and related code
3. **P3 (API Cleanup)**: Remove `linkToken` optional parameters from frontend

**Estimated Time**: 2-3 hours (P1: 30 min, P2: 1.5 hr, P3: 30 min)

---

## Prerequisites

- [ ] Read [spec.md](spec.md) and [data-model.md](data-model.md)
- [ ] Verify you are on branch `003-remove-deprecated-code`
- [ ] Run `dotnet build` and ensure zero errors/warnings
- [ ] Run `dotnet test Karamel.Web.Tests` and ensure baseline (251 passing, 9 skipped)
- [ ] Run `npm run test:run` (in `Karamel.Web/wwwroot`) and ensure baseline (222 tests)

---

## Phase 1: Remove BroadcastPlaylistUpdatedAsync (P1)

**Goal**: Remove no-op method from SignalR bridge interface and implementation

### Step 1.1: Check Current Usage

```powershell
# Verify the method is indeed a no-op
Get-Content Karamel.Web\Services\SignalRPlaylistBridge.cs | Select-String "BroadcastPlaylistUpdatedAsync" -Context 2,5

# Find all call sites
Get-ChildItem -Recurse -Filter *.cs | Select-String "BroadcastPlaylistUpdatedAsync"
# Expected: ~3-5 matches in PlaylistEffects.cs
```

### Step 1.2: Remove Call Sites

**File**: `Karamel.Web/Store/Playlist/PlaylistEffects.cs`

**Find and remove** all lines like:
```csharp
await _signalRBridge.BroadcastPlaylistUpdatedAsync();
```

**Affected methods** (typical):
- `AddSongToPlaylistEffect`
- `RemoveItemEffect`
- `ReorderPlaylistEffect`

### Step 1.3: Remove Interface Method

**File**: `Karamel.Web/Services/ISignalRPlaylistBridge.cs`

**Remove**:
```csharp
Task BroadcastPlaylistUpdatedAsync();
```

### Step 1.4: Remove Implementation

**File**: `Karamel.Web/Services/SignalRPlaylistBridge.cs`

**Remove**:
```csharp
public Task BroadcastPlaylistUpdatedAsync() => Task.CompletedTask;
```

### Step 1.5: Verify P1 Changes

```powershell
# Build should succeed
dotnet build

# Search for leftovers (should return zero results)
Get-ChildItem -Recurse -Filter *.cs | Select-String "BroadcastPlaylistUpdated"

# Run frontend tests
dotnet test Karamel.Web.Tests
# Expected: ≥251 passing, 9 skipped (same baseline)

# Commit P1 changes
git add -A
git commit -m "Remove deprecated BroadcastPlaylistUpdatedAsync no-op method"
```

---

## Phase 2: Remove LinkToken Backend (P2)

**Goal**: Remove LinkToken property, database column, repository/service methods, and hub filter

### Step 2.1: Remove LinkTokenHubFilter

**File**: `Karamel.Backend/Filters/LinkTokenHubFilter.cs`

**Action**: Delete entire file

**File**: `Karamel.Backend/Program.cs`

**Find and remove** hub filter registration:
```csharp
// Remove this line (or similar):
options.AddFilter<LinkTokenHubFilter>();
```

### Step 2.2: Remove ITokenService Methods

**File**: `Karamel.Backend/Services/ITokenService.cs`

**Remove**:
```csharp
string GenerateLinkToken(Guid sessionId);
bool ValidateLinkToken(Guid sessionId, string token);
```

**File**: `Karamel.Backend/Services/TokenService.cs`

**Remove** implementations of the above methods

### Step 2.3: Remove ISessionRepository Method

**File**: `Karamel.Backend/Repositories/ISessionRepository.cs`

**Remove**:
```csharp
Task<Session?> GetByLinkTokenAsync(string linkToken);
```

**File**: `Karamel.Backend/Repositories/SessionRepository.cs`

**Remove** implementation of `GetByLinkTokenAsync`

### Step 2.4: Remove Session Model Property

**File**: `Karamel.Backend/Models/Session.cs`

**Remove**:
```csharp
public string? LinkToken { get; set; }
```

### Step 2.5: Update SessionsController

**File**: `Karamel.Backend/Controllers/SessionsController.cs`

**In `Create` method**, remove:
1. `LinkToken = _tokenService.GenerateLinkToken(session.Id)` assignment
2. `linkToken = session.LinkToken` from response object

**Before**:
```csharp
var session = new Session
{
    // ...
    LinkToken = _tokenService.GenerateLinkToken(session.Id)
};
return Ok(new { sessionId, adminToken, singerToken, linkToken });
```

**After**:
```csharp
var session = new Session
{
    // ...
    // LinkToken removed
};
return Ok(new { sessionId, adminToken, singerToken });
```

### Step 2.6: Create EF Core Migration

```powershell
# Generate migration
dotnet ef migrations add RemoveLinkToken --project Karamel.Backend
# Creates: Karamel.Backend/Migrations/[Timestamp]_RemoveLinkToken.cs
```

**Edit migration file** to add data migration:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Data migration: copy LinkToken → AdminToken (safety)
    migrationBuilder.Sql(
        "UPDATE Sessions SET AdminToken = LinkToken WHERE AdminToken IS NULL AND LinkToken IS NOT NULL"
    );
    
    // Schema migration: drop column
    migrationBuilder.DropColumn(
        name: "LinkToken",
        table: "Sessions"
    );
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>(
        name: "LinkToken",
        table: "Sessions",
        type: "nvarchar(max)",
        nullable: true
    );
    migrationBuilder.Sql("UPDATE Sessions SET LinkToken = AdminToken");
}
```

### Step 2.7: Test Migration Locally

```powershell
# Apply migration to local SQLite database
dotnet ef database update --project Karamel.Backend

# Verify schema
dotnet run --project Karamel.Backend
# In another terminal:
Invoke-WebRequest -Uri http://localhost:5000/api/sessions -Method POST | ConvertFrom-Json
# Should return sessionId, adminToken, singerToken (NO linkToken)
```

### Step 2.8: Update Log Messages

**Search for log messages** mentioning "LinkToken":
```powershell
Get-ChildItem Karamel.Backend -Recurse -Filter *.cs | Select-String "LinkToken" -Context 1,1
```

**Replace** with "AdminToken" or remove if irrelevant

### Step 2.9: Verify P2 Changes

```powershell
# Build should succeed
dotnet build

# Run backend tests
dotnet test Karamel.Backend.Tests -v minimal
# Expected: All tests pass (~40s run time)

# Run frontend tests (verify no frontend breakage)
dotnet test Karamel.Web.Tests
# Expected: ≥251 passing, 9 skipped

# Search for LinkToken leftovers in backend
Get-ChildItem Karamel.Backend -Recurse -Filter *.cs | Select-String "LinkToken"
# Should return zero results (except migration history)

# Commit P2 changes
git add -A
git commit -m "Remove deprecated LinkToken property and database column"
```

---

## Phase 3: Remove LinkToken Frontend Parameters (P3)

**Goal**: Remove optional `linkToken` parameters from frontend service interfaces

### Step 3.1: Update ISignalRConnectionManager

**File**: `Karamel.Web/Services/ISignalRConnectionManager.cs`

**Before**:
```csharp
Task InitializeAsync(Guid sessionId, string? adminToken, string? singerToken, string? linkToken);
```

**After**:
```csharp
Task InitializeAsync(Guid sessionId, string? adminToken, string? singerToken);
```

**Update implementation** in `SignalRConnectionManager.cs` to match signature

### Step 3.2: Update ISessionApiClient

**File**: `Karamel.Web/Services/ISessionApiClient.cs`

**Before**:
```csharp
Task UploadLibraryToServerAsync(IEnumerable<Song> songs, Guid sessionId, string? linkToken);
```

**After**:
```csharp
Task UploadLibraryToServerAsync(IEnumerable<Song> songs, Guid sessionId);
```

**Update implementation** in `SessionApiClient.cs` to match signature

### Step 3.3: Update ISessionStorageService

**File**: `Karamel.Web/Services/ISessionStorageService.cs`

**Before**:
```csharp
Task<string> GenerateSessionUrlAsync(Guid sessionId, string? linkToken, string? adminToken, string? singerToken);
```

**After**:
```csharp
Task<string> GenerateSessionUrlAsync(Guid sessionId, string? adminToken, string? singerToken);
```

**Update implementation** in `SessionStorageService.cs` to match signature

### Step 3.4: Remove Backward Compatibility Code

**File**: `Karamel.Web/Pages/Home.razor`

**Find and remove** (or similar):
```csharp
// Backward compatibility: read linkToken from session JSON
if (sessionJson.TryGetProperty("linkToken", out var linkTokenProp))
{
    linkToken = linkTokenProp.GetString();
}
```

**Update InitializeAsync call**:
```csharp
// Old: await _connectionManager.InitializeAsync(sessionId, adminToken, singerToken, linkToken);
await _connectionManager.InitializeAsync(sessionId, adminToken, singerToken);
```

### Step 3.5: Update All Call Sites

**Search for all calls**:
```powershell
# Find InitializeAsync calls
Get-ChildItem Karamel.Web -Recurse -Filter *.cs | Select-String "InitializeAsync" -Context 0,2

# Find UploadLibraryToServerAsync calls
Get-ChildItem Karamel.Web -Recurse -Filter *.cs | Select-String "UploadLibraryToServerAsync" -Context 0,2

# Find GenerateSessionUrlAsync calls
Get-ChildItem Karamel.Web -Recurse -Filter *.cs | Select-String "GenerateSessionUrlAsync" -Context 0,2
```

**Update each call site** to remove linkToken argument

### Step 3.6: Verify P3 Changes

```powershell
# Build should succeed
dotnet build

# Run frontend tests
dotnet test Karamel.Web.Tests
# Expected: ≥251 passing, 9 skipped

# Search for linkToken leftovers in frontend
Get-ChildItem Karamel.Web -Recurse -Filter *.cs | Select-String "linkToken"
# Should return zero results

# Commit P3 changes
git add -A
git commit -m "Remove deprecated linkToken parameters from frontend services"
```

---

## Final Verification

### Test All Functionality

```powershell
# Full build
dotnet clean
dotnet build
# Expected: Zero errors, zero warnings

# Frontend tests
dotnet test Karamel.Web.Tests
# Expected: ≥251 passing, 9 skipped

# Backend tests (request user to run manually)
dotnet test Karamel.Backend.Tests -v minimal
# Expected: All tests pass

# JavaScript tests
cd Karamel.Web\wwwroot
npm run test:run
cd ..\..
# Expected: All 222 tests pass
```

### Code Search for Leftovers

```powershell
# Search for "LinkToken" (PascalCase)
Get-ChildItem -Recurse -Filter *.cs | Select-String "LinkToken" | Where-Object { $_.Line -notmatch "Migration" }
# Expected: Zero results (except migration history files)

# Search for "linkToken" (camelCase)
Get-ChildItem -Recurse -Filter *.cs | Select-String "linkToken" | Where-Object { $_.Line -notmatch "Migration" }
# Expected: Zero results

# Search for "BroadcastPlaylist"
Get-ChildItem -Recurse -Filter *.cs | Select-String "BroadcastPlaylist"
# Expected: Zero results

# Check log messages and XML docs
Get-ChildItem -Recurse -Filter *.cs | Select-String "/// .* LinkToken" 
# Expected: Zero results (or update to AdminToken)
```

### Manual Testing (Optional)

1. Run app: `dotnet run --project Karamel.Web`
2. Create new session (Home page)
3. Add songs to playlist (SingerView)
4. Verify QR code URL format: `?session={guid}&token={token}` (no linkToken)
5. Connect from another device via QR code
6. Verify SignalR synchronization works

---

## Success Criteria Checklist

- [ ] SC-001: Zero compilation errors or warnings (`dotnet build`)
- [ ] SC-002: ≥251 C# frontend tests passing, 9 skipped (`dotnet test Karamel.Web.Tests`)
- [ ] SC-003: All C# backend tests passing (`dotnet test Karamel.Backend.Tests`)
- [ ] SC-004: ≥222 JavaScript tests passing (`npm run test:run`)
- [ ] SC-005: Migration successfully removes LinkToken column
- [ ] SC-006: `dotnet ef migrations list` shows RemoveLinkToken migration
- [ ] SC-007: Session API response has adminToken/singerToken, NOT linkToken
- [ ] SC-008: Code search for "LinkToken"/"linkToken" returns zero results (except migrations)
- [ ] SC-009: No "DEPRECATED" comments remain
- [ ] SC-010: QR code URLs use `?session={guid}&token={token}` format only

---

## Rollback Plan

If issues arise after deployment:

### Rollback Migration
```powershell
# Revert to previous migration
dotnet ef database update [PreviousMigrationName] --project Karamel.Backend
# This re-adds the LinkToken column and copies AdminToken back
```

### Rollback Code
```powershell
# Revert commits
git log --oneline  # Find commit hashes
git revert <commit-hash-p3> <commit-hash-p2> <commit-hash-p1>
```

---

## Next Steps

After all phases complete:
1. Update [DEVELOPMENT_PLAN.md](../../DEVELOPMENT_PLAN.md) to mark cleanup as complete
2. Consider additional cleanups identified during this work
3. Document any architectural learnings in an ADR if significant patterns emerged

---

## Troubleshooting

### "Method not found" errors after removing BroadcastPlaylistUpdatedAsync
- **Cause**: Missed a call site in PlaylistEffects.cs
- **Fix**: Search for `BroadcastPlaylist` and remove all invocations

### Migration fails with "column does not exist"
- **Cause**: Column already removed manually or in a previous migration
- **Fix**: Check `dotnet ef migrations list` for duplicate migrations; remove if needed

### Tests fail with "Parameter count mismatch"
- **Cause**: Call site still passing linkToken parameter after interface signature changed
- **Fix**: Search for method name and update all call sites to remove linkToken argument

### SignalR authorization fails after LinkTokenHubFilter removal
- **Cause**: Hub methods not validating adminToken/singerToken correctly
- **Fix**: Verify `PlaylistHub` methods call `ValidateAdminToken`/`ValidateSingerToken`

---

## Estimated Timeline

| Phase | Tasks | Estimated Time |
|-------|-------|----------------|
| P1 | Remove BroadcastPlaylistUpdatedAsync | 30 minutes |
| P2 | Remove LinkToken backend (model, repo, service, migration) | 1.5 hours |
| P3 | Remove linkToken parameters from frontend | 30 minutes |
| Testing | Full test suite + manual verification | 30 minutes |
| **Total** | | **2.5-3 hours** |

---

## References

- [Feature Spec](spec.md) - Full requirements and user stories
- [Data Model](data-model.md) - Entity and schema changes
- [API Contract](contracts/api-contract.md) - Backend API response changes
- [Frontend Services Contract](contracts/frontend-services-contract.md) - Service interface changes
- [Research](research.md) - Migration strategy and verification approach
