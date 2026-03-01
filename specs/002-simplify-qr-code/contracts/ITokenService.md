# Contract: ITokenService

**Namespace**: `Karamel.Backend.Services`  
**File**: `Karamel.Backend/Services/ITokenService.cs`

---

## Interface (New)

```csharp
namespace Karamel.Backend.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// Generates a role-based link token.
        /// Token format: Base64url({role}|{hmac-sha256-of-sessionId:role})
        /// The sessionId is NOT embedded in the token — it is provided separately
        /// during validation via the URL query parameter.
        /// </summary>
        string GenerateLinkToken(Guid sessionId, string role = "admin");

        /// <summary>
        /// Validates a link token given the session ID from the URL.
        /// Parses the token, recomputes the HMAC using the provided sessionId,
        /// and returns the role on success.
        /// </summary>
        /// <param name="token">The URL-safe base64-encoded token string.</param>
        /// <param name="sessionId">The session ID from the URL query parameter.</param>
        /// <returns>
        ///   (role, isValid=true) on success.
        ///   ("", isValid=false) on any failure (malformed, tampered, wrong session).
        /// </returns>
        (string role, bool isValid) ValidateLinkToken(string token, Guid sessionId);
    }
}
```

---

## Breaking Change vs. Old Interface

| | Old | New |
|-|-----|-----|
| `ValidateLinkToken` params | `string token` | `string token, Guid sessionId` |
| `ValidateLinkToken` returns | `(Guid sessionId, string role, bool isValid)` | `(string role, bool isValid)` |
| Token format | `{sessionId}\|{role}\|{hmac}` | `{role}\|{hmac}` |
| HMAC input | `$"{sessionId}\|{role}"` | `$"{sessionId}:{role}"` |

All callers must be updated atomically. No backward compatibility is provided (sessions are short-lived; 30-min TTL means no saved tokens survive a deployment).

---

## Caller Update Guide

### LinkTokenActionFilter (REST filter)

```csharp
// Old
var (tokenSessionId, _, isValid) = tokenService.ValidateLinkToken(token);
if (!isValid || tokenSessionId != sessionId) { ... }

// New
var (_, isValid) = tokenService.ValidateLinkToken(token, sessionId);
if (!isValid) { ... }
```

### LinkTokenHubFilter (SignalR filter)

```csharp
// Old
var (tokenSessionId, role, isValid) = _tokenService.ValidateLinkToken(token);
if (!isValid || tokenSessionId != sessionId) { ... }

// New
var (role, isValid) = _tokenService.ValidateLinkToken(token, sessionId);
if (!isValid) { ... }
```

---

## Security Guarantees

1. **Forgery resistance**: HMAC-SHA256 with server-side HMAC secret. An attacker without the secret cannot produce a valid HMAC for any `(sessionId, role)` pair.
2. **Cross-session resistance**: HMAC input includes sessionId from URL. Copying a token from one session URL to another session's URL will fail HMAC verification.
3. **Role tamper resistance**: Changing `role` in the token payload invalidates the HMAC.
4. **Timing-safe comparison**: `AreEqualConstantTime` prevents timing-based HMAC oracle attacks.
5. **Short-lived**: Session TTL of 30 minutes limits the window for any token credential.

---

## Token URL Length Impact

| Scenario | Old URL length | New URL length | Reduction |
|----------|--------------|----------------|-----------|
| Typical singer URL | ~240 chars | ~190 chars | ~50 chars (~21%) |

This reduction lowers QR code complexity by approximately one error-correction level tier on many phone cameras, improving scan reliability.
