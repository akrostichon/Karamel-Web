# Contract: SongListItemDto (backend → frontend library API)

**Direction**: Backend → Frontend (GET /api/sessions/{id}/library)  
**File**: `Karamel.Backend/Controllers/LibraryDtos.cs`, `Karamel.Web/Contracts/SongDto.cs`

---

## Change Summary

`SongListItemDto` (backend) already carries `MetadataJson`. No change to the backend DTO record.  
`ConvertJsonToSong` (frontend) must be extended to parse `durationSeconds` from MetadataJson.

---

## Backend SongListItemDto — no change

```csharp
// Already exists — unchanged
public record SongListItemDto(
    Guid Id,
    Guid SessionId,
    string Artist,
    string Title,
    string? MetadataJson,
    DateTime AddedAt
);
```

---

## Frontend ConvertJsonToSong — extended parsing

New parsing block added inside the MetadataJson parsing section of `ConvertJsonToSong`:

```csharp
// After existing mediaType / extension extraction:
int durationSeconds = 0;
if (metadata.TryGetProperty("durationSeconds", out var durProp) &&
    durProp.TryGetInt32(out var dur) && dur > 0)
{
    durationSeconds = dur;
}
```

And the returned `Song`:
```csharp
return new Song
{
    // ... existing fields ...
    DurationSeconds = durationSeconds,   // NEW
};
```

---

## Wire format example (library API response item)

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sessionId": "...",
  "artist": "ABBA",
  "title": "Waterloo",
  "metadataJson": "{\"durationSeconds\":175}",
  "addedAt": "2026-03-01T14:00:00Z"
}
```
