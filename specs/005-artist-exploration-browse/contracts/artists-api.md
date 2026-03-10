# API Contract: Artists Endpoint

*Phase 1 output — REST endpoint contract for the artist browse feature.*

---

## GET /api/sessions/{sessionId}/library/artists

### Purpose
Returns the full artist list for a session's library, ordered alphabetically, with song counts. Used exclusively by the artist browse mode in `LibrarySearch`.

### Route

```
GET /api/sessions/{sessionId:guid}/library/artists
```

### Authentication
No auth required (read-only, same as `GET /library`). The `sessionId` in the route is the access control boundary — callers can only retrieve artists for a session they know the GUID of (same trust model as the library page endpoint).

### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sessionId` | `Guid` | ✅ | The session GUID |

### Query Parameters
None.

### Success Response

**HTTP 200 OK**

```json
[
  { "name": "ABBA",  "songCount": 12 },
  { "name": "AC/DC", "songCount":  8 },
  { "name": "Adele", "songCount":  5 }
]
```

**Response body**: JSON array of artist summary objects, sorted case-insensitively A→Z.

| Field | Type | Description |
|-------|------|-------------|
| `name` | `string` | Artist display name (as stored in library) |
| `songCount` | `integer` | Number of songs in the session for this artist |

**Empty library response** (no songs uploaded yet):

```json
[]
```

### Error Responses

| Status | Condition |
|--------|-----------|
| `500 Internal Server Error` | Unexpected database or server error |

*(A 404 is NOT returned for unknown sessions — an empty array is returned instead, consistent with the library page endpoint behaviour.)*

---

## Frontend Call Site

**Interface method** (`ISessionApiClient`):
```csharp
/// <summary>
/// Fetches the full artist list for a session from the backend API.
/// Returns an empty list if the session has no library or the request fails.
/// </summary>
Task<IReadOnlyList<ArtistItem>> FetchArtistsAsync(Guid sessionId);
```

**HTTP call**:
```
GET {baseUrl}/api/sessions/{sessionId}/library/artists
```

No `X-Link-Token` header required (read-only endpoint, no admin authorization needed).

---

## Serialization Contract

Backend DTO → JSON → Frontend DTO mapping:

| Backend (`ArtistSummaryDto`) | JSON field | Frontend (`ArtistDto`) |
|------------------------------|------------|------------------------|
| `string Name` | `"name"` | `string Name` |
| `int SongCount` | `"songCount"` | `int SongCount` |

Both sides use `[JsonPropertyName("camelCase")]` attributes. No enums cross this boundary.

---

## Performance Expectations

- Typical response time: < 100 ms (simple GROUP BY on an indexed `SessionId` + `Artist` column)
- Typical payload size: < 10 KB for a 200-artist library
- Called once per session visit to browse mode; result is cached in `LibraryState.Artists`
