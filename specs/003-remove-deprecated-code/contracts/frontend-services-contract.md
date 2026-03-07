# Frontend Service Contracts: Remove LinkToken Parameters

**Date**: 2026-03-07  
**Feature**: Remove optional `linkToken` parameters from service methods  
**Priority**: P3

## Affected Interfaces

### 1. ISignalRConnectionManager

**Location**: `Karamel.Web/Services/ISignalRConnectionManager.cs`

**Method**: `InitializeAsync`

**Before**:
```csharp
/// <summary>
/// Initializes the SignalR connection for the given session.
/// </summary>
Task InitializeAsync(
    Guid sessionId, 
    string? adminToken, 
    string? singerToken, 
    string? linkToken  // ← DEPRECATED, to be removed
);
```

**After**:
```csharp
/// <summary>
/// Initializes the SignalR connection for the given session.
/// </summary>
Task InitializeAsync(
    Guid sessionId, 
    string? adminToken, 
    string? singerToken
    // linkToken parameter REMOVED
);
```

**Call Sites**: Update all callers to remove the 4th parameter

---

### 2. ISessionApiClient

**Location**: `Karamel.Web/Services/ISessionApiClient.cs`

**Method**: `UploadLibraryToServerAsync`

**Before**:
```csharp
/// <summary>
/// Uploads the local song library to the backend for the given session.
/// </summary>
Task UploadLibraryToServerAsync(
    IEnumerable<Song> songs, 
    Guid sessionId, 
    string? linkToken  // ← DEPRECATED, to be removed
);
```

**After**:
```csharp
/// <summary>
/// Uploads the local song library to the backend for the given session.
/// </summary>
Task UploadLibraryToServerAsync(
    IEnumerable<Song> songs, 
    Guid sessionId
    // linkToken parameter REMOVED
);
```

**Implementation Note**: The method already uses `SessionState.Value.CurrentSession.AdminToken` for authorization header (`X-Admin-Token`), so the `linkToken` parameter was never used.

---

### 3. ISessionStorageService

**Location**: `Karamel.Web/Services/ISessionStorageService.cs`

**Method**: `GenerateSessionUrlAsync`

**Before**:
```csharp
/// <summary>
/// Generates a session URL for QR code display or sharing.
/// </summary>
Task<string> GenerateSessionUrlAsync(
    Guid sessionId, 
    string? linkToken,   // ← DEPRECATED, to be removed
    string? adminToken, 
    string? singerToken
);
```

**After**:
```csharp
/// <summary>
/// Generates a session URL for QR code display or sharing.
/// </summary>
Task<string> GenerateSessionUrlAsync(
    Guid sessionId, 
    string? adminToken, 
    string? singerToken
    // linkToken parameter REMOVED
);
```

**Implementation Note**: Generated QR code URLs use format `?session={guid}&token={adminToken}`, never included `linkToken` query parameter.

---

### 4. ISignalRPlaylistBridge

**Location**: `Karamel.Web/Services/ISignalRPlaylistBridge.cs`

**Method**: `BroadcastPlaylistUpdatedAsync`

**Before**:
```csharp
public interface ISignalRPlaylistBridge
{
    Task AddSongToPlaylistAsync(Guid sessionId, Guid songId, string? singerName);
    Task RemoveItemAsync(Guid sessionId, Guid itemId);
    Task ReorderPlaylistAsync(Guid sessionId, int from, int to);
    Task BroadcastPlaylistUpdatedAsync();  // ← DEPRECATED NO-OP, entire method removed
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

**Rationale**: This method returns `Task.CompletedTask` (no-op). SignalR hub methods already broadcast updates automatically.

---

## Implementation Changes

### Home.razor Backward Compatibility

**Before**:
```csharp
// Backward compatibility: read linkToken from session JSON
if (sessionJson.TryGetProperty("linkToken", out var linkTokenProp))
{
    linkToken = linkTokenProp.GetString();
}

await _connectionManager.InitializeAsync(sessionId, adminToken, singerToken, linkToken);
```

**After**:
```csharp
// linkToken backward compatibility code REMOVED

await _connectionManager.InitializeAsync(sessionId, adminToken, singerToken);
```

---

### PlaylistEffects.cs Call Site Removal

**Before** (example from `AddSongToPlaylistEffect`):
```csharp
await _signalRBridge.AddSongToPlaylistAsync(sessionId, action.Song.Id, action.SingerName);
await _signalRBridge.BroadcastPlaylistUpdatedAsync();  // ← NO-OP, to be removed
```

**After**:
```csharp
await _signalRBridge.AddSongToPlaylistAsync(sessionId, action.Song.Id, action.SingerName);
// BroadcastPlaylistUpdatedAsync call REMOVED (SignalR hub methods broadcast automatically)
```

**Affected Effects**:
- `AddSongToPlaylistEffect`
- `RemoveItemEffect`
- `ReorderPlaylistEffect`
- (Any other effect that calls `BroadcastPlaylistUpdatedAsync`)

---

## Breaking Changes

### For Frontend Code

**Breaking**: ✅ YES (signature changes)

**Migration Path**:
1. **Remove linkToken argument** from all calls to:
   - `InitializeAsync(sessionId, adminToken, singerToken, ~~linkToken~~)` → `InitializeAsync(sessionId, adminToken, singerToken)`
   - `UploadLibraryToServerAsync(songs, sessionId, ~~linkToken~~)` → `UploadLibraryToServerAsync(songs, sessionId)`
   - `GenerateSessionUrlAsync(sessionId, ~~linkToken~~, adminToken, singerToken)` → `GenerateSessionUrlAsync(sessionId, adminToken, singerToken)`

2. **Remove all calls** to `BroadcastPlaylistUpdatedAsync()` (no-op method)

**Why Safe**:
- `linkToken` parameters were optional (`string?`) and never used by implementations
- `BroadcastPlaylistUpdatedAsync()` was a no-op (returned `Task.CompletedTask`)
- Actual synchronization happens via SignalR hub method invocations (`AddItemAsync`, `RemoveItemAsync`, `ReorderAsync`)

---

## Testing Contract Changes

### C# Frontend Tests

**Test**: Verify service methods work without linkToken parameter

```csharp
[Fact]
public async Task InitializeAsync_WithoutLinkToken_Succeeds()
{
    // Arrange
    var sessionId = Guid.NewGuid();
    var adminToken = "admin123";
    var singerToken = "singer456";
    
    // Act
    await _connectionManager.InitializeAsync(sessionId, adminToken, singerToken);
    // Note: NO linkToken parameter (breaking change)
    
    // Assert
    _connectionManager.IsMainTab.Should().BeTrue();
}
```

**Test**: Verify BroadcastPlaylistUpdatedAsync is removed

```csharp
[Fact]
public void ISignalRPlaylistBridge_DoesNotHaveBroadcastMethod()
{
    // Assert
    var interfaceType = typeof(ISignalRPlaylistBridge);
    var method = interfaceType.GetMethod("BroadcastPlaylistUpdatedAsync");
    
    method.Should().BeNull("BroadcastPlaylistUpdatedAsync should be removed");
}
```

---

## Summary

### Removed Parameters
- `ISignalRConnectionManager.InitializeAsync`: Remove `linkToken` parameter (4th param)
- `ISessionApiClient.UploadLibraryToServerAsync`: Remove `linkToken` parameter (3rd param)
- `ISessionStorageService.GenerateSessionUrlAsync`: Remove `linkToken` parameter (2nd param)

### Removed Methods
- `ISignalRPlaylistBridge.BroadcastPlaylistUpdatedAsync`: Remove entire method (no-op)

### Call Sites Updated
- `Home.razor`: Remove backward compatibility code for linkToken
- `PlaylistEffects.cs`: Remove all calls to `BroadcastPlaylistUpdatedAsync()`

### No Functional Impact
- All removed parameters/methods were unused or no-op
- SignalR synchronization works the same way (hub methods broadcast automatically)
- Session initialization works with just `adminToken` and `singerToken`
