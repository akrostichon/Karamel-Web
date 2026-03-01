# Contract: SongUploadDto (MetadataJson extension)

**Direction**: Frontend → Backend (library upload)  
**File**: `Karamel.Web/Contracts/SongUploadDto.cs` + `Karamel.Backend/Controllers/LibraryDtos.cs`

---

## Change Summary

`durationSeconds` is NOT added as a new top-level field on `SongUploadDto`. Instead it is embedded inside the `metadataJson` JSON blob. This avoids a backend model change and keeps the upload DTO stable.

---

## Frontend SongUploadDto — unchanged record signature

```csharp
public record SongUploadDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("metadataJson")] string? MetadataJson
);
```

## `ConvertSongToUploadDto` — updated logic

Before (video only):
```csharp
if (s.MediaType == MediaType.Video)
{
    var metadata = new { mediaType = "video", extension = s.VideoExtension };
    metadataJson = JsonSerializer.Serialize(metadata);
}
```

After (all songs, duration always included when non-zero):
```csharp
// Always serialise metadata so durationSeconds travels to the backend
var metadata = s.MediaType == MediaType.Video
    ? (object)new { mediaType = "video", extension = s.VideoExtension, durationSeconds = s.DurationSeconds }
    : (object)new { durationSeconds = s.DurationSeconds };

metadataJson = s.DurationSeconds > 0 || s.MediaType == MediaType.Video
    ? JsonSerializer.Serialize(metadata)
    : null;  // Avoid writing empty JSON for songs with no metadata
```

---

## Backend SongUploadDto — no change needed

The backend `SongUploadDto` (`Karamel.Backend/Controllers/LibraryDtos.cs`) already has `MetadataJson`. No backend change required for upload.

---

## Wire format example

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "artist": "ABBA",
  "title": "Waterloo",
  "metadataJson": "{\"durationSeconds\":175}"
}
```
