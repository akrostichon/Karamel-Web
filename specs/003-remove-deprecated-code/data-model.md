# Data Model: Remove Deprecated Methods and Properties

**Date**: 2026-03-07  
**Feature**: Remove deprecated code (LinkToken, BroadcastPlaylistUpdatedAsync)  
**Status**: Design complete

## Overview

This feature **removes** deprecated properties, methods, and classes from the domain model and service interfaces. No new entities are introduced. The changes simplify the architecture by eliminating dead code paths and reducing cognitive load.

---

## Entity Changes

### 1. Session (Backend Domain Model)

**Location**: `Karamel.Backend/Models/Session.cs`  
**Change Type**: Remove property  
**Priority**: P2

**Before**:
```csharp
public class Session
{
    public Guid Id { get; set; }
    public string AdminToken { get; set; } = null!;
    public string SingerToken { get; set; } = null!;
    public string? LinkToken { get; set; }  // <-- DEPRECATED, to be removed
    public DateTime CreatedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    // ... other properties
}
```

**After**:
```csharp
public class Session
{
    public Guid Id { get; set; }
    public string AdminToken { get; set; } = null!;
    public string SingerToken { get; set; } = null!;
    // LinkToken property REMOVED
    public DateTime CreatedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    // ... other properties
}
```

**Validation Rules**: None changed (AdminToken and SingerToken validation remains the same)  
**Database Migration**: Required (see Migration Strategy section)  
**Relationships**: None affected (Session → Playlist relationship unchanged)

---

### 2. Sessions Table (Database Schema)

**Location**: `Karamel.Backend/Data/ApplicationDbContext.cs` (schema inferred from Session entity)  
**Change Type**: Remove column  
**Priority**: P2

**Before**:
```sql
CREATE TABLE Sessions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    AdminToken NVARCHAR(MAX) NOT NULL,
    SingerToken NVARCHAR(MAX) NOT NULL,
    LinkToken NVARCHAR(MAX) NULL,  -- <-- DEPRECATED, to be removed
    CreatedAt DATETIME2 NOT NULL,
    LastHeartbeat DATETIME2 NOT NULL,
    Theme NVARCHAR(50) NULL
);
```

**After**:
```sql
CREATE TABLE Sessions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    AdminToken NVARCHAR(MAX) NOT NULL,
    SingerToken NVARCHAR(MAX) NOT NULL,
    -- LinkToken column REMOVED
    CreatedAt DATETIME2 NOT NULL,
    LastHeartbeat DATETIME2 NOT NULL,
    Theme NVARCHAR(50) NULL
);
```

**Migration Strategy**:
1. **Data preservation**: Copy existing `LinkToken` values to `AdminToken` where `AdminToken IS NULL` (safety measure)
2. **Column drop**: Use `migrationBuilder.DropColumn("LinkToken", "Sessions")`
3. **Rollback support**: `Down()` method re-adds column and copies `AdminToken` back to `LinkToken`

**Migration File**: `Karamel.Backend/Migrations/[Timestamp]_RemoveLinkToken.cs` (to be generated via `dotnet ef migrations add RemoveLinkToken`)

---

## Interface Changes

### 3. ISessionRepository (Backend Repository Interface)

**Location**: `Karamel.Backend/Repositories/ISessionRepository.cs`  
**Change Type**: Remove method  
**Priority**: P2

**Before**:
```csharp
public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id);
    Task<Session?> GetByLinkTokenAsync(string linkToken);  // <-- DEPRECATED, to be removed
    Task<IEnumerable<Session>> GetExpiredSessionsAsync(TimeSpan ttl);
    Task CreateAsync(Session session);
    Task UpdateAsync(Session session);
    Task DeleteAsync(Guid id);
}
```

**After**:
```csharp
public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id);
    // GetByLinkTokenAsync method REMOVED
    Task<IEnumerable<Session>> GetExpiredSessionsAsync(TimeSpan ttl);
    Task CreateAsync(Session session);
    Task UpdateAsync(Session session);
    Task DeleteAsync(Guid id);
}
```

**Implementation**: `SessionRepository.cs` must also remove the corresponding implementation  
**Call Sites**: Verify no code calls `GetByLinkTokenAsync` (should be replaced by `GetByIdAsync` or token validation)

---

### 4. ITokenService (Backend Service Interface)

**Location**: `Karamel.Backend/Services/ITokenService.cs`  
**Change Type**: Remove methods  
**Priority**: P2

**Before**:
```csharp
public interface ITokenService
{
    string GenerateAdminToken(Guid sessionId);
    string GenerateSingerToken(Guid sessionId);
    string GenerateLinkToken(Guid sessionId);  // <-- DEPRECATED, to be removed
    bool ValidateAdminToken(Guid sessionId, string token);
    bool ValidateSingerToken(Guid sessionId, string token);
    bool ValidateLinkToken(Guid sessionId, string token);  // <-- DEPRECATED, to be removed
}
```

**After**:
```csharp
public interface ITokenService
{
    string GenerateAdminToken(Guid sessionId);
    string GenerateSingerToken(Guid sessionId);
    // GenerateLinkToken method REMOVED
    bool ValidateAdminToken(Guid sessionId, string token);
    bool ValidateSingerToken(Guid sessionId, string token);
    // ValidateLinkToken method REMOVED
}
```

**Implementation**: `TokenService.cs` must also remove the corresponding implementations  
**Call Sites**: Verify `SessionsController` and `LinkTokenHubFilter` no longer call these methods

---

### 5. ISignalRPlaylistBridge (Frontend Service Interface)

**Location**: `Karamel.Web/Services/ISignalRPlaylistBridge.cs`  
**Change Type**: Remove method  
**Priority**: P1

**Before**:
```csharp
public interface ISignalRPlaylistBridge
{
    Task AddSongToPlaylistAsync(Guid sessionId, Guid songId, string? singerName);
    Task RemoveItemAsync(Guid sessionId, Guid itemId);
    Task ReorderPlaylistAsync(Guid sessionId, int from, int to);
    Task BroadcastPlaylistUpdatedAsync();  // <-- DEPRECATED NO-OP, to be removed
}
```

**After**:
```csharp
public interface ISignalRPlaylistBridge
{
    Task AddSongToPlaylistAsync(Guid sessionId, Guid songId, string? singerName);
    Task RemoveItemAsync(Guid sessionId, Guid itemId);
    Task ReorderPlaylistAsync(Guid sessionId, int from, int to);
    // BroadcastPlaylistUpdatedAsync method REMOVED
}
```

**Implementation**: `SignalRPlaylistBridge.cs` must also remove the no-op implementation  
**Call Sites**: Remove all invocations in `PlaylistEffects.cs` (after AddSongToPlaylist, RemoveItem, ReorderPlaylist effects)

---

### 6. LinkTokenHubFilter (Backend SignalR Filter)

**Location**: `Karamel.Backend/Filters/LinkTokenHubFilter.cs`  
**Change Type**: Remove entire class  
**Priority**: P2

**Before**: Entire class exists for legacy LinkToken validation  
**After**: File deleted, class removed from `Program.cs` filter registration  

**Impact**: SignalR authorization continues to work via `AdminToken`/`SingerToken` validation in `PlaylistHub` methods

---

## Service Method Signature Changes

### 7. Frontend Service Optional Parameters

**Priority**: P3

**Affected Interfaces**:

#### ISignalRConnectionManager.InitializeAsync
**Before**: `Task InitializeAsync(Guid sessionId, string? adminToken, string? singerToken, string? linkToken)`  
**After**: `Task InitializeAsync(Guid sessionId, string? adminToken, string? singerToken)`

#### ISessionApiClient.UploadLibraryToServerAsync
**Before**: `Task UploadLibraryToServerAsync(IEnumerable<Song> songs, Guid sessionId, string? linkToken)`  
**After**: `Task UploadLibraryToServerAsync(IEnumerable<Song> songs, Guid sessionId)`

#### ISessionStorageService.GenerateSessionUrlAsync
**Before**: `Task<string> GenerateSessionUrlAsync(Guid sessionId, string? linkToken, string? adminToken, string? singerToken)`  
**After**: `Task<string> GenerateSessionUrlAsync(Guid sessionId, string? adminToken, string? singerToken)`

**Impact**: Remove `linkToken` parameter from all method signatures and all call sites

---

## Controller Response Changes

### 8. SessionsController.Create Response

**Location**: `Karamel.Backend/Controllers/SessionsController.cs`  
**Change Type**: Remove response field  
**Priority**: P2

**Before**:
```csharp
return Ok(new {
    sessionId = session.Id,
    adminToken = session.AdminToken,
    singerToken = session.SingerToken,
    linkToken = session.LinkToken  // <-- DEPRECATED, to be removed
});
```

**After**:
```csharp
return Ok(new {
    sessionId = session.Id,
    adminToken = session.AdminToken,
    singerToken = session.SingerToken
    // linkToken field REMOVED
});
```

---

## Migration Strategy

### EF Core Migration: RemoveLinkToken

**Command**: `dotnet ef migrations add RemoveLinkToken --project Karamel.Backend`

**Up() Method**:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Step 1: Data migration (safety - copy LinkToken to AdminToken if AdminToken is null)
    migrationBuilder.Sql(
        "UPDATE Sessions SET AdminToken = LinkToken WHERE AdminToken IS NULL AND LinkToken IS NOT NULL"
    );
    
    // Step 2: Schema migration (drop column)
    migrationBuilder.DropColumn(
        name: "LinkToken",
        table: "Sessions"
    );
}
```

**Down() Method (Rollback)**:
```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // Re-add column
    migrationBuilder.AddColumn<string>(
        name: "LinkToken",
        table: "Sessions",
        type: "nvarchar(max)",
        nullable: true
    );
    
    // Copy AdminToken back to LinkToken (best-effort restore)
    migrationBuilder.Sql(
        "UPDATE Sessions SET LinkToken = AdminToken"
    );
}
```

**Testing**:
1. Run migration locally with SQLite: `dotnet ef database update --project Karamel.Backend`
2. Verify Sessions table schema with `SELECT * FROM Sessions`
3. Test session creation API - verify response has no `linkToken` field
4. Rollback test: `dotnet ef database update [PreviousMigration]` - verify column returns

---

## State Transitions

**None** - This feature does not introduce new state transitions. Existing session/playlist state flows remain unchanged:
- Session creation → Active → Expired (TTL-based cleanup)
- PlaylistItem: Queued → UpNext → NowPlaying → Completed (unchanged)

---

## Summary

### Removals
- **1 Entity Property**: `Session.LinkToken`
- **1 Database Column**: `Sessions.LinkToken`
- **1 Repository Method**: `ISessionRepository.GetByLinkTokenAsync()`
- **2 Service Methods**: `ITokenService.GenerateLinkToken()`, `ITokenService.ValidateLinkToken()`
- **1 Service Method**: `ISignalRPlaylistBridge.BroadcastPlaylistUpdatedAsync()`
- **1 Hub Filter Class**: `LinkTokenHubFilter`
- **3 Service Parameters**: `linkToken` parameter from `InitializeAsync`, `UploadLibraryToServerAsync`, `GenerateSessionUrlAsync`
- **1 Controller Response Field**: `linkToken` from `SessionsController.Create` response

### Additions
- **1 EF Core Migration**: `[Timestamp]_RemoveLinkToken.cs`

### No Changes
- Domain concepts (Session, Playlist, Song, Singer remain unchanged)
- Relationships between entities (Session → Playlist one-to-one remains)
- Authorization logic (AdminToken/SingerToken validation unchanged)
- SignalR hub methods (PlaylistHub interface unchanged)
- Playlist status flow (Queued → UpNext → NowPlaying → Completed unchanged)
