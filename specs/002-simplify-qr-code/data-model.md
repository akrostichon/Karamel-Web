# Data Model: Token Format

## Context

This is a **value object** change, not a database schema change. No migrations are required.
The link token is a transient, stateless credential computed on-demand by `TokenService` and
validated by `IHubFilter` and `IAsyncActionFilter`. It is not persisted.

---

## Token Value Object

### Old Format (being removed)

```
Raw payload:  {sessionId}|{role}|{hmac}
              └── 36 ──┘ └─5+─┘ └──43──┘  = ~86 chars raw
Encoded:      Base64url(raw payload)        ≈ 115 chars
```

| Field      | Type   | Example                                | Notes                        |
|------------|--------|----------------------------------------|------------------------------|
| sessionId  | GUID   | `104906d0-f2ef-4bc7-a2f6-dacdf8a5b2d3` | Redundant — already in URL   |
| role       | string | `admin` \| `singer`                    | Authorization level          |
| hmac       | string | 43-char Base64url                      | HMAC-SHA256 of `{sid}\|{role}` |

---

### New Format (implemented by this plan)

```
Raw payload:  {role}|{hmac}
              └─5+─┘ └──43──┘  = ~49 chars raw
Encoded:      Base64url(raw payload)  ≈ 68 chars
```

| Field | Type   | Example           | Notes                                     |
|-------|--------|-------------------|-------------------------------------------|
| role  | string | `admin` \| `singer` | Authorization level                     |
| hmac  | string | 43-char Base64url | HMAC-SHA256 of `{sessionId}:{role}` where sessionId comes from URL |

**HMAC input**: `$"{sessionId}:{role}"` using `:` as separator (not `|`) to avoid ambiguity with the token payload delimiter.

---

## Validation Rules

| Rule | Description |
|------|-------------|
| Non-empty | Token must be non-empty string |
| Part count = 2 | Decoded token must split into exactly 2 parts on `\|` |
| Role known | Role must be recognizable (currently: `admin`, `singer`) |
| HMAC matches | `ComputeHmac($"{sessionId}:{role}")` must equal the HMAC in the token |
| Constant-time comparison | HMAC comparison uses byte-level XOR to prevent timing attacks |

---

## State Transitions

Tokens are stateless — no lifecycle / state machine applies.

- Token issued: at session creation (`POST /api/sessions` response includes `linkToken`)
- Token used: on every authenticated hub invocation and REST API call
- Token expires: implicitly when the session TTL (30 min) expires; the HMAC secret is the only "state"

---

## No Database Impact

- No new tables, columns, or indexes
- No EF Core migration required
- Session table in `Karamel.Backend/Data/AppDbContext.cs` is unchanged
