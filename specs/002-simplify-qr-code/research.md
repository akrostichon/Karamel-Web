# Research: Simplify QR Code by Removing SessionId from Token

## Status: Complete — all unknowns resolved

---

## 1. Current Token Format (verified from source)

**File**: `Karamel.Backend/Services/TokenService.cs`

- **Generation**: `Base64({sessionId}|{role}|{hmac})`  
  - Payload passed to HMAC: `$"{sessionId}|{role}"`
- **Validation**: `ValidateLinkToken(string token)` → `(Guid sessionId, string role, bool isValid)`  
  - Extracts sessionId from token, re-computes HMAC, compares

**Token length analysis** (current):
```
GUID (36) + "|" (1) + "admin" (5) + "|" (1) + HMAC-SHA256-Base64url (43) = 86 raw chars
Base64url(86 bytes) ≈ 115 chars
```

**URL length** (current): `https://…?session={36}&token={115}` ≈ 217–247 chars (including base path)

---

## 2. New Token Format

- **Generation**: `Base64({role}|{hmac})`  
  - Payload passed to HMAC: `$"{sessionId}:{role}"` (`:` separator avoids collision with `|` in payload)
- **Validation**: `ValidateLinkToken(string token, Guid sessionId)` → `(string role, bool isValid)`  
  - sessionId comes from caller (already in URL), not from token

**New token length**:
```
"admin" (5) + "|" (1) + HMAC-SHA256-Base64url (43) = 49 raw chars
Base64url(49 bytes) ≈ 68 chars
```

**Reduction**: ~47 fewer chars in token → ~47 fewer chars in QR URL (**~22% reduction**)

---

## 3. HMAC Separator Change

**Decision**: Change HMAC input from `$"{sessionId}|{role}"` to `$"{sessionId}:{role}"`

**Rationale**: In the old format, the `|` separator was shared between the token payload and the HMAC input. In the new format, the token payload uses `|` while the HMAC input uses `:` — making it impossible to confuse the two contexts and hardening against crafted inputs where a role value might contain `|`.

**Alternatives considered**:
- Keep `|` in HMAC input too — works but is slightly more confusing to reason about
- Use a full HMAC over `sessionId.ToByteArray() + roleBytes` — overkill, no benefit

---

## 4. Interface Contract Change (ITokenService)

**Current signature**:
```csharp
(Guid sessionId, string role, bool isValid) ValidateLinkToken(string token);
```

**New signature**:
```csharp
(string role, bool isValid) ValidateLinkToken(string token, Guid sessionId);
```

**Callers that must be updated**:
| File | Change |
|------|--------|
| `Filters/LinkTokenActionFilter.cs` | Pass `sessionId` (already extracted from route); remove `tokenSessionId != sessionId` check |
| `Filters/LinkTokenHubFilter.cs` | Pass `sessionId` (already extracted from hub args); remove `tokenSessionId != sessionId` check |
| `Backend.Tests/TokenServiceTests.cs` | Update all tests to new signature and return type |
| `Backend.Tests/PlaylistHubTests.cs` | Integration tests: no direct call to `ValidateLinkToken` — token generation only via `TestServerFactory`, likely unaffected at call site |

**Decision**: Update `ITokenService` and all callers atomically.

---

## 5. Breaking Change Assessment

**Impact**: All existing link tokens become invalid after deployment.

**Mitigation**: Session TTL is 30 minutes. No tokens survive a deployment anyway.

**Decision**: Accepted per the plan's explicit statement: *"Breaking change acceptable"*.

---

## 6. LinkTokenActionFilter Logging Adjustment

The current filter logs a comparison between the received token and a freshly generated "expected" token:
```csharp
var expectedToken = tokenService.GenerateLinkToken(sessionId); // always admin
```
This was always wrong for singer tokens (always compared against an admin token). After the change, we keep the `GenerateLinkToken(sessionId)` call for admin-token logging but simplify the validation branch — the `tokenSessionId != sessionId` check is eliminated because sessionId is no longer in the token.

**Decision**: Simplify logging; keep existing log line shapes (same LogInformation call, adjust variable names).

---

## 7. Security Analysis

**Unchanged**:
- HMAC-SHA256 with server-side secret key
- URL-safe base64 encoding
- Constant-time comparison (`AreEqualConstantTime`)
- Role tamper protection (changing role in token breaks HMAC)

**Improved**:
- sessionId no longer redundantly embedded in token (less attack surface)
- HMAC separator is now distinct from payload separator

**No weakening**: The security proof remains: an attacker cannot forge a valid token without knowing the secret key, and cannot substitute one session's token into another session's URL (the HMAC includes sessionId from the URL, so swapping the session parameter makes the HMAC check fail).

---

## 8. Frontend Impact

**None.** The token is opaque to all JavaScript modules. `signalRBridge.js`, `sessionBridge.js`, and the Blazor `ISessionApiClient` simply forward the token string they received from the backend session creation response. The shape of the token and its length are irrelevant to them.

---

## 9. No Unknowns Remaining

All NEEDS CLARIFICATION items from Technical Context have been resolved above.
