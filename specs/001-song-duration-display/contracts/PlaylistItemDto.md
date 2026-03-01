# Contract: PlaylistItemDto (backend → frontend SignalR)

**Direction**: Backend → Frontend (SignalR `PlaylistHub`)  
**File**: `Karamel.Web/Contracts/PlaylistItemDto.cs`, `Karamel.Backend/Hubs/PlaylistHub.cs`

---

## Change Summary

`PlaylistItemDto` gains a `durationSeconds` field. The backend `PlaylistHub` populates it by parsing the song's `MetadataJson` when building playlist responses.

---

## Frontend PlaylistItemDto — updated record

```csharp
public record PlaylistItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("songId")] string? SongId,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("singerName")] string? SingerName,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("durationSeconds")] int DurationSeconds = 0   // NEW
);
```

`DurationSeconds` defaults to `0` so existing SignalR messages that lack the field continue to deserialise correctly.

---

## Backend PlaylistHub — updated item projection

Wherever the hub builds playlist item objects (typically a `Select` projection), add the `durationSeconds` field:

```csharp
// New private helper
private static int ParseDuration(string? metadataJson)
{
    if (string.IsNullOrWhiteSpace(metadataJson)) return 0;
    try
    {
        using var doc = JsonDocument.Parse(metadataJson);
        return doc.RootElement.TryGetProperty("durationSeconds", out var p)
               && p.TryGetInt32(out var d) ? d : 0;
    }
    catch { return 0; }
}

// Updated projection (example — exact lambda depends on existing hub code)
items.Select(i => new
{
    i.Id,
    i.SongId,
    i.Artist,
    i.Title,
    i.SingerName,
    i.Position,
    i.Status,
    durationSeconds = ParseDuration(i.MetadataJson)   // NEW
})
```

---

## Wire format example (SignalR message item)

```json
{
  "id": "playlist-item-guid",
  "songId": "song-guid",
  "artist": "ABBA",
  "title": "Waterloo",
  "singerName": "Alice",
  "position": 1,
  "status": 1,
  "durationSeconds": 175
}
```

---

## Backward Compatibility

Existing SignalR clients (prior to this feature) that do not have `durationSeconds` in the deserialization model will simply ignore the new field — no breaking change.

After this feature is deployed, old cached pages that lack the new frontend code will deserialise `durationSeconds` as `0` (default), which is handled by the "zero = hidden" display rule.
