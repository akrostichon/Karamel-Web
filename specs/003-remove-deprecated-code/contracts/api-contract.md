# Backend API Contract: Session Management

**Date**: 2026-03-07  
**Feature**: Remove LinkToken from session responses  
**Priority**: P2

## Affected Endpoints

### POST /api/sessions

**Purpose**: Create a new karaoke session

**Request**: (unchanged)
```http
POST /api/sessions HTTP/1.1
Content-Type: application/json

{
  "theme": "dark"  // optional
}
```

**Response (BEFORE)**:
```json
{
  "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "adminToken": "a1b2c3d4e5f6...",
  "singerToken": "x1y2z3w4v5u6...",
  "linkToken": "a1b2c3d4e5f6..."  // ← DEPRECATED (duplicate of adminToken)
}
```

**Response (AFTER)**:
```json
{
  "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "adminToken": "a1b2c3d4e5f6...",
  "singerToken": "x1y2z3w4v5u6..."
  // linkToken field REMOVED
}
```

**Status Codes**: (unchanged)
- `200 OK`: Session created successfully
- `500 Internal Server Error`: Database or token generation failure

**Breaking Change**: ✅ YES
- Clients expecting `linkToken` in the response will no longer receive it
- **Mitigation**: Clients should use `adminToken` instead (functionally identical)

---

## Authentication & Authorization (Unchanged)

All other endpoints continue to use `adminToken` or `singerToken` for authentication:

### GET /api/sessions/{sessionId}/library
**Authorization**: `X-Admin-Token` header (unchanged)

### POST /api/sessions/{sessionId}/library
**Authorization**: `X-Admin-Token` header (unchanged)

### POST /api/sessions/{sessionId}/heartbeat
**Authorization**: `X-Admin-Token` header (unchanged)

### SignalR PlaylistHub
**Authorization**: `adminToken` or `singerToken` in connection headers (unchanged)

---

## Token Format (Unchanged)

**AdminToken** and **SingerToken** are HMAC-SHA256 hashes of the session ID:
```csharp
HMACSHA256(secretKey, sessionId.ToString())
```

Base64-encoded, 44 characters. Example: `a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6`

**LinkToken was identical to AdminToken** (same generation logic), which is why it's being removed as redundant.

---

## Implementation Changes

### SessionsController.cs

**Before**:
```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateSessionRequest? request)
{
    var session = new Session
    {
        Id = Guid.NewGuid(),
        AdminToken = _tokenService.GenerateAdminToken(sessionId),
        SingerToken = _tokenService.GenerateSingerToken(sessionId),
        LinkToken = _tokenService.GenerateLinkToken(sessionId),  // ← REMOVE
        Theme = request?.Theme,
        CreatedAt = DateTime.UtcNow,
        LastHeartbeat = DateTime.UtcNow
    };

    await _sessionRepository.CreateAsync(session);

    return Ok(new
    {
        sessionId = session.Id,
        adminToken = session.AdminToken,
        singerToken = session.SingerToken,
        linkToken = session.LinkToken  // ← REMOVE from response
    });
}
```

**After**:
```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateSessionRequest? request)
{
    var session = new Session
    {
        Id = Guid.NewGuid(),
        AdminToken = _tokenService.GenerateAdminToken(sessionId),
        SingerToken = _tokenService.GenerateSingerToken(sessionId),
        // LinkToken property REMOVED
        Theme = request?.Theme,
        CreatedAt = DateTime.UtcNow,
        LastHeartbeat = DateTime.UtcNow
    };

    await _sessionRepository.CreateAsync(session);

    return Ok(new
    {
        sessionId = session.Id,
        adminToken = session.AdminToken,
        singerToken = session.SingerToken
        // linkToken field REMOVED
    });
}
```

---

## Testing Contract Changes

### Integration Test (Backend)

**Test**: `SessionApiTests.Create_ReturnsSessionWithoutLinkToken`

```csharp
[Fact]
public async Task Create_ReturnsSessionWithoutLinkToken()
{
    // Arrange
    var client = _factory.CreateClient();
    var content = new StringContent("{\"theme\":\"dark\"}", Encoding.UTF8, "application/json");

    // Act
    var response = await client.PostAsync("/api/sessions", content);
    var json = await response.Content.ReadAsStringAsync();
    var sessionData = JsonSerializer.Deserialize<JsonElement>(json);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    sessionData.GetProperty("sessionId").GetGuid().Should().NotBeEmpty();
    sessionData.GetProperty("adminToken").GetString().Should().NotBeNullOrEmpty();
    sessionData.GetProperty("singerToken").GetString().Should().NotBeNullOrEmpty();
    sessionData.TryGetProperty("linkToken", out _).Should().BeFalse();  // ← NEW assertion
}
```

---

## Migration Path for Consumers

If any external code depends on `linkToken`:

1. **Replace `response.linkToken` with `response.adminToken`** in client code
2. **Why safe**: LinkToken was always identical to AdminToken (same HMAC-SHA256 value)
3. **No functional change**: Authorization still works the same way

**Example** (JavaScript client):
```javascript
// BEFORE
const { sessionId, linkToken } = await createSession();
connectToSignalR(sessionId, linkToken);

// AFTER
const { sessionId, adminToken } = await createSession();
connectToSignalR(sessionId, adminToken);
```

---

## Summary

- **Removed response field**: `linkToken` from `POST /api/sessions`
- **No new endpoints**: This is purely a removal
- **Authorization unchanged**: All endpoints continue to use `adminToken` / `singerToken`
- **Breaking change**: YES, but safe (linkToken was redundant duplicate of adminToken)
- **Client migration**: Replace `linkToken` with `adminToken` (1:1 substitution)
