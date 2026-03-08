# API Contract: Library Search Endpoint

**Feature**: Smart Search — Fuzzy Matching, Relevance Ranking, and Spelling Suggestions  
**Contract type**: REST HTTP + SignalR RPC  
**Date**: 2026-03-08

---

## 1. REST Endpoint: `GET /api/sessions/{sessionId}/library`

### 1.1 Request

| Parameter | Location | Type | Required | Description |
|---|---|---|---|---|
| `sessionId` | Path | `Guid` | Yes | Session identifier |
| `page` | Query | `int` | No (default 1) | 1-based page number |
| `pageSize` | Query | `int` | No (default 50) | Number of items per page |
| `search` | Query | `string?` | No | Search query; triggers fuzzy matching when ≥ 3 chars |
| `sort` | Query | `string?` | No | Sort key (`"artist"` or `"addedAt"`); ignored when `search` is set |

**Authentication**: No token required for GET (read-only).

### 1.2 Response: Success (200 OK)

**CHANGED**: The response body is now a JSON object (previously a plain array).

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "artist": "Queen",
      "title": "Bohemian Rhapsody",
      "metadataJson": null,
      "addedAt": "2026-03-08T10:00:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "suggestions": []
}
```

**Response when zero results (with suggestions)**:

```json
{
  "items": [],
  "totalCount": 0,
  "page": 1,
  "pageSize": 50,
  "suggestions": [
    { "text": "Bohemian Rhapsody", "sourceField": "title" },
    { "text": "Queen", "sourceField": "artist" }
  ]
}
```

**Response headers (unchanged)**:
- `X-Total-Count: {totalCount}` — retained for backward compatibility.

### 1.3 Field Descriptions

| Field | Type | Description |
|---|---|---|
| `items` | `SongListItemDto[]` | Relevance-ordered results (ExactTitle first); alphabetical within each tier |
| `totalCount` | `long` | Total matching songs (capped at 500 for fuzzy pass) |
| `page` | `int` | Current page number (mirrors request) |
| `pageSize` | `int` | Page size used (mirrors request) |
| `suggestions` | `SearchSuggestionDto[]` | 0–3 suggestions; **empty unless `items` is empty** (FR-006) |

### 1.4 `SearchSuggestionDto`

| Field | Type | Description |
|---|---|---|
| `text` | `string` | The suggested alternative search term |
| `sourceField` | `"artist"` \| `"title"` | Which field the suggestion was derived from |

### 1.5 Ordering Contract

When `search` is provided:
1. `ExactTitle` results (case-insensitive exact match on `title`) — alphabetical within tier
2. `PartialTitle` results (title contains query as substring) — alphabetical within tier
3. `ArtistOnly` results (artist contains query, title does not) — alphabetical within tier
4. `FuzzyMatch` results (within edit-distance threshold, no substring match) — alphabetical within tier, lowest edit distance first on a tie

When no `search` is provided: `ORDER BY Artist ASC, Title ASC` (DB-level, unchanged).

### 1.6 Pagination Consistency

For a given `search` query, all pages of "Load More" results are slices of the
same relevance-sorted candidate set (R3 decision). Page 2 results continue
where page 1 left off in the relevance ordering.

**Example** — query `"yesterday"`, pageSize 2, library has 5 results:
- Page 1: `[{tier:0, "Yesterday"}, {tier:1, "Yesterday Once More"}]`
- Page 2: `[{tier:2, "Smells Like Yesterday"}, {tier:3, "Ystrday"}]`
- Page 3: `[{tier:3, "Yesterdy"}]`

---

## 2. SignalR RPC: `GetLibraryPage`

**Hub**: `PlaylistHub`  
**Method**: `GetLibraryPage`

### 2.1 Invocation (unchanged signature)

```javascript
const result = await hubConnection.invoke(
  'GetLibraryPage',
  sessionId,   // string (Guid)
  page,        // number
  pageSize,    // number
  search,      // string | null
  sort         // string | null
);
```

### 2.2 Return Value (CHANGED — `suggestions` added)

```typescript
{
  items:       SongListItem[];   // same schema as REST
  page:        number;
  pageSize:    number;
  totalCount:  number;
  suggestions: SearchSuggestion[];  // NEW — empty unless items is empty
}
```

```typescript
interface SearchSuggestion {
  text:        string;           // "Bohemian Rhapsody"
  sourceField: "artist"|"title";
}
```

### 2.3 Existing `SearchLibrary` RPC (CHANGED — relevance ordering)

```javascript
const results = await hubConnection.invoke(
  'SearchLibrary',
  sessionId,   // string
  query,       // string
  maxResults   // number (default 20)
);
```

Return value (object array — unchanged shape):
```typescript
{ id: string, artist: string, title: string, metadataJson: string|null }[]
```

**Behaviour change**: Results are now relevance-ordered (ExactTitle first)
rather than sorted by artist. No client-side changes required for this RPC.

---

## 3. `ISessionApiClient` Extension (C# frontend service)

The existing `FetchLibraryPageAsync` method already returns `JsonElement`.
No signature change is needed — the returned JSON will now include
`suggestions` which `LibraryEffects` reads and dispatches.

```csharp
// UNCHANGED signature
Task<JsonElement> FetchLibraryPageAsync(
    Guid sessionId,
    int page = 1,
    int pageSize = 50,
    string? search = null,
    string? sort = null);
```

The JavaScript function `fetchLibraryPage` in `signalRBridge.js` is updated
to read `data.items` and `data.totalCount` from the object body (instead of
the array body + header) and to pass `suggestions` through:

```javascript
// signalRBridge.js — REST fallback, updated section
const data = await resp.json();   // now an object: { items, totalCount, page, pageSize, suggestions }
const items = data.items ?? [];
const total = data.totalCount ?? parseInt(resp.headers.get('X-Total-Count') || '0');
const suggestions = data.suggestions ?? [];
return { items, page, pageSize, totalCount: total, suggestions };
```

---

## 4. `ISongRepository` Interface Change

```csharp
// UNCHANGED method signature
Task<PagedResult<SongListItemDto>> GetPageAsync(
    Guid sessionId, int page, int pageSize, string? search, string? sort);
```

`PagedResult<T>` is extended with `Suggestions`:

```csharp
// MODIFIED
public record PagedResult<T>(
    IEnumerable<T>                    Items,
    int                               Page,
    int                               PageSize,
    long                              TotalCount,
    IReadOnlyList<SearchSuggestionDto> Suggestions   // NEW — empty list when Items non-empty
);
```

---

## 5. Breaking-Change Analysis

| Consumer | Impact | Migration |
|---|---|---|
| `LibraryController.GetPage` (REST) | Body format changes from array to object | Update controller to return `LibraryResponseDto` |
| `signalRBridge.js` REST fallback | Reads `data.items` instead of treating body as array | Single-line update (see §3) |
| `signalRBridge.js` SignalR path | New `suggestions` field added | Pass-through; no structural change |
| `LibraryEffects.cs` C# | Reads `JsonElement`; must extract `suggestions` | Add `TryGetProperty("suggestions", ...)` |
| `PlaylistHub.GetLibraryPage` | Add `suggestions` to anonymous return object | One-line addition |
| Existing `LibraryApiTests` | Assert on items array directly | Update to read `items` from response object |

No external (out-of-repo) API consumers are known.
