---
description: 'JavaScript↔C# serialization patterns and DTO conventions for Blazor WebAssembly'
applyTo: '**/*.js, **/Contracts/*.cs'
---

# JavaScript↔C# Serialization Guidelines

Critical patterns for maintaining type safety and data integrity across the JavaScript↔C# boundary in Karamel-Web Blazor WebAssembly application.

## Project Context

- **Architecture**: Blazor WebAssembly frontend with JavaScript ES modules
- **Serialization**: System.Text.Json with camelCase property naming
- **Data Flow**: JavaScript (File System API) → C# (Blazor) → Backend API → Database
- **Critical Requirement**: All DTO properties must match JavaScript object keys exactly

## Core Principles

### 1. DTO Property Names Must Match JavaScript Object Keys

**CRITICAL**: JSON property names are **case-sensitive** and must match exactly between JavaScript and C#.

#### ✅ Correct Pattern
```csharp
// SongDto.cs
public record SongDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("mediaType")] string? MediaType,
    [property: JsonPropertyName("videoFileName")] string? VideoFileName
);
```

```javascript
// fileAccess.js
const song = {
    id: crypto.randomUUID(),
    artist: "Adele",
    title: "Skyfall",
    mediaType: "video",          // ✅ Matches JsonPropertyName("mediaType")
    videoFileName: "Adele - Skyfall.mp4"  // ✅ Matches JsonPropertyName("videoFileName")
};
```

#### ❌ Common Mistakes
```csharp
// Missing JsonPropertyName attribute - defaults to PascalCase
public record SongDto(
    string MediaType,  // ❌ Will serialize as "MediaType" not "mediaType"
    string VideoFileName  // ❌ Will serialize as "VideoFileName" not "videoFileName"
);
```

### 2. Enum Serialization: Use Strings Not Integers

**CRITICAL**: When passing enums across JavaScript↔C# boundary, **always use string representation**.

#### ❌ Wrong: Integer Serialization
```csharp
// ConvertSongToDto - WRONG
MediaType: (int)s.MediaType  // ❌ Serializes as 0 or 1
```

```javascript
// JavaScript receives:
{ mediaType: 1 }  // ❌ Backend API rejects this
```

#### ✅ Correct: String Serialization
```csharp
// ConvertSongToDto - CORRECT
MediaType: s.MediaType == Models.MediaType.Video ? "video" : "mp3cdg"  // ✅ Serializes as string
```

```javascript
// JavaScript receives:
{ mediaType: "video" }  // ✅ Backend API accepts this
```

### 3. DTO Converter Pattern

All DTO conversions must be **bidirectional** and **lossless** for non-sensitive data.

#### Standard Converter Methods

**Song → SongDto** (for JavaScript consumption):
```csharp
public static SongDto ConvertSongToDto(Song s) => new(
    Id: s.Id.ToString(),
    Artist: s.Artist,
    Title: s.Title,
    Mp3FileName: s.Mp3FileName,
    CdgFileName: s.CdgFileName,
    VideoFileName: s.VideoFileName,
    VideoExtension: s.VideoExtension,
    MediaType: s.MediaType == Models.MediaType.Video ? "video" : "mp3cdg",  // String representation
    Path: s.Path,
    FullPath: s.FullPath,
    // ... other properties
);
```

**SongDto → Song** (from JavaScript):
```csharp
public static Song ConvertDtoToSong(SongDto dto)
{
    // Parse string back to enum
    var mediaType = Models.MediaType.Mp3Cdg; // Default for backward compatibility
    if (!string.IsNullOrEmpty(dto.MediaType))
    {
        if (dto.MediaType.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            mediaType = Models.MediaType.Video;
        }
    }

    return new Song
    {
        Id = Guid.Parse(dto.Id),
        Artist = dto.Artist,
        Title = dto.Title,
        MediaType = mediaType,  // Parsed from string
        VideoFileName = dto.VideoFileName,
        VideoExtension = dto.VideoExtension,
        // ... other properties
    };
}
```

**Song → SongUploadDto** (for backend API - privacy-aware):
```csharp
public static SongUploadDto ConvertSongToUploadDto(Song s)
{
    // Sanitize: Strip file paths and local-only data
    // Include MediaType in MetadataJson for backend storage
    var metadata = new
    {
        mediaType = s.MediaType == Models.MediaType.Video ? "video" : "mp3cdg",  // String representation
        videoExtension = s.VideoExtension,
        duration = s.Duration,
        // NO file paths - privacy requirement
    };
    
    var metadataJson = JsonSerializer.Serialize(metadata);
    
    return new SongUploadDto
    {
        Artist = s.Artist,
        Title = s.Title,
        MetadataJson = metadataJson  // Structured metadata as JSON string
    };
}
```

**Backend JSON → Song** (from backend API - restore from MetadataJson):
```csharp
public static Song ConvertJsonToSong(JsonElement s)
{
    var artist = s.GetProperty("artist").GetString() ?? string.Empty;
    var title = s.GetProperty("title").GetString() ?? string.Empty;
    
    // Parse MetadataJson to restore MediaType
    var mediaType = Models.MediaType.Mp3Cdg; // Default
    if (s.TryGetProperty("metadataJson", out var metaJson))
    {
        var metaJsonStr = metaJson.GetString();
        if (!string.IsNullOrEmpty(metaJsonStr))
        {
            var metaDoc = JsonDocument.Parse(metaJsonStr);
            if (metaDoc.RootElement.TryGetProperty("mediaType", out var mediaTypeEl))
            {
                var mediaTypeStr = mediaTypeEl.GetString();
                if (mediaTypeStr?.Equals("video", StringComparison.OrdinalIgnoreCase) == true)
                {
                    mediaType = Models.MediaType.Video;
                }
            }
        }
    }
    
    return new Song
    {
        Artist = artist,
        Title = title,
        MediaType = mediaType,
        VideoExtension = /* parse from MetadataJson */,
        // File paths remain empty - privacy requirement
    };
}
```

## Common Patterns

### JavaScript Object to C# DTO

**JavaScript Creation** (fileAccess.js):
```javascript
function buildVideoSong(file) {
    const song = {
        id: crypto.randomUUID(),
        artist: extractedArtist,
        title: extractedTitle,
        mediaType: 'video',  // Lowercase string
        videoFileName: file.name,
        videoExtension: getExtension(file.name),
        path: relativePath,
        fullPath: fullPath
    };
    return song;
}
```

**C# Consumption**:
```csharp
// Blazor component receives JavaScript array
var songsJson = await fileAccessModule.InvokeAsync<JsonElement[]>("scanDirectory");

// Convert to Song objects
var songs = songsJson.Select(s => SongDto.ConvertDtoToSong(
    JsonSerializer.Deserialize<SongDto>(s.GetRawText())
)).ToList();
```

### C# Object to JavaScript

**C# Preparation**:
```csharp
// Convert Song to SongDto for JavaScript
var dto = SongDto.ConvertSongToDto(song);
```

**JavaScript Consumption** (player.js):
```javascript
export async function loadSong(songDto) {
    // songDto has camelCase properties matching JsonPropertyName attributes
    const { mediaType, videoFileName, mp3FileName, cdgFileName } = songDto;
    
    if (mediaType === 'video') {
        await loadVideoFile(videoFileName);
    } else {
        await loadSongFiles(mp3FileName, cdgFileName);
    }
}
```

## Testing Serialization

### Required Tests

**1. Roundtrip Test** (JavaScript → C# → JavaScript):
```csharp
[Fact]
public void ConvertDtoToSong_And_Back_Preserves_Video_Properties()
{
    // Arrange: Create SongDto as JavaScript would
    var originalDto = new SongDto(
        Id: Guid.NewGuid().ToString(),
        Artist: "Adele",
        Title: "Skyfall",
        MediaType: "video",  // String representation
        VideoFileName: "Adele - Skyfall.mp4",
        VideoExtension: ".mp4",
        // ...
    );
    
    // Act: Convert to Song and back to DTO
    var song = SongDto.ConvertDtoToSong(originalDto);
    var resultDto = SongDto.ConvertSongToDto(song);
    
    // Assert: All properties preserved
    Assert.Equal("video", resultDto.MediaType);
    Assert.Equal(Models.MediaType.Video, song.MediaType);
    Assert.Equal(originalDto.VideoFileName, resultDto.VideoFileName);
}
```

**2. Enum String Conversion Test**:
```csharp
[Theory]
[InlineData(Models.MediaType.Video, "video")]
[InlineData(Models.MediaType.Mp3Cdg, "mp3cdg")]
public void ConvertSongToDto_Serializes_MediaType_As_String(Models.MediaType mediaType, string expected)
{
    var song = new Song { MediaType = mediaType };
    
    var dto = SongDto.ConvertSongToDto(song);
    
    Assert.Equal(expected, dto.MediaType);
}
```

**3. Backward Compatibility Test**:
```csharp
[Fact]
public void ConvertDtoToSong_Handles_Missing_MediaType()
{
    // Simulate old JavaScript code that doesn't send mediaType
    var dto = new SongDto(MediaType: null, /* ... */);
    
    var song = SongDto.ConvertDtoToSong(dto);
    
    // Should default to Mp3Cdg for backward compatibility
    Assert.Equal(Models.MediaType.Mp3Cdg, song.MediaType);
}
```

## Troubleshooting

### Issue: DTO Properties Not Deserializing

**Symptom**: C# receives `null` or default values for properties that JavaScript sent.

**Diagnosis**:
```csharp
#if DEBUG
// Add logging to converter
Console.WriteLine($"DTO MediaType: '{dto.MediaType}', VideoFileName: '{dto.VideoFileName}'");
#endif
```

**Common Causes**:
1. Missing `[JsonPropertyName]` attribute
2. Case mismatch between JavaScript key and JsonPropertyName value
3. Missing property in SongDto record definition

**Fix**: Ensure SongDto property exists with matching JsonPropertyName:
```csharp
[property: JsonPropertyName("videoFileName")] string? VideoFileName  // Must match JavaScript key exactly
```

### Issue: Enum Serializes as Integer

**Symptom**: Backend API rejects requests with error about invalid MediaType value.

**Diagnosis**: Check network tab - request shows `{ "mediaType": 1 }` instead of `{ "mediaType": "video" }`.

**Fix**: Update converter to use string representation:
```csharp
// ❌ WRONG
MediaType: (int)s.MediaType

// ✅ CORRECT
MediaType: s.MediaType == Models.MediaType.Video ? "video" : "mp3cdg"
```

### Issue: Library Appears Empty After Scan

**Symptom**: JavaScript creates songs correctly, but LibraryState is empty or shows wrong MediaType.

**Diagnosis**:
```javascript
// JavaScript side
console.log('Created song:', song);  // Shows correct mediaType: 'video'
```

```csharp
// C# side
#if DEBUG
Console.WriteLine($"Received MediaType: {song.MediaType}");  // Shows Mp3Cdg
#endif
```

**Common Cause**: DTO missing video properties - C# deserializer silently ignores unknown JavaScript properties.

**Fix**: Add missing properties to SongDto record definition.

## Privacy-Aware Serialization

### File Path Sanitization

**Rule**: File paths MUST NOT be sent to backend or stored in database.

**Pattern**:
```csharp
public static SongUploadDto ConvertSongToUploadDto(Song s)
{
    // ✅ INCLUDE: Artist, Title, public metadata
    // ❌ EXCLUDE: Mp3FileName, CdgFileName, VideoFileName, Path, FullPath
    
    return new SongUploadDto
    {
        Artist = s.Artist,
        Title = s.Title,
        MetadataJson = CreateSanitizedMetadata(s)  // No file paths
    };
}
```

**Rationale**: Privacy Architecture - file paths reveal user's file system structure and should remain in main tab only.

### Enrichment Pattern

**Problem**: Secondary tabs need file paths for playback, but backend doesn't store them.

**Solution**: Enrichment from main tab's LibraryState:
```csharp
public async Task<IEnumerable<Song>> EnrichSongsWithLibraryFiles(IEnumerable<Song> songs)
{
    return songs.Select(song =>
    {
        var libraryMatch = LibraryState.Value.Songs.FirstOrDefault(s => s.Id == song.Id);
        if (libraryMatch == null) return song;
        
        // Copy file paths from LibraryState (main tab only)
        if (libraryMatch.MediaType == MediaType.Video)
        {
            song.VideoFileName = libraryMatch.VideoFileName;
        }
        else
        {
            song.Mp3FileName = libraryMatch.Mp3FileName;
            song.CdgFileName = libraryMatch.CdgFileName;
        }
        
        return song;
    });
}
```

## Best Practices Checklist

When adding new properties to DTOs:

- [ ] Add `[JsonPropertyName("camelCaseName")]` attribute to C# property
- [ ] Use lowercase camelCase in JsonPropertyName value
- [ ] Match exact casing of JavaScript object key
- [ ] For enums: serialize as string, not integer
- [ ] Add converter methods for bidirectional conversion
- [ ] Write roundtrip test to verify serialization
- [ ] Test backward compatibility (missing property handling)
- [ ] Check privacy requirements (file paths, sensitive data)
- [ ] Document in DTO class XML comments
- [ ] Update all converter methods (ConvertDtoToSong, ConvertSongToDto, ConvertSongToUploadDto)

## References

- **SongDto Definition**: [Karamel.Web/Contracts/SongDto.cs](../../Karamel.Web/Contracts/SongDto.cs)
- **JavaScript Song Creation**: [Karamel.Web/wwwroot/js/fileAccess.js](../../Karamel.Web/wwwroot/js/fileAccess.js)
- **DTO Converter Tests**: [Karamel.Web.Tests/SongDtoConverterTests.cs](../../Karamel.Web.Tests/SongDtoConverterTests.cs)
- **Privacy Architecture**: [.github/copilot-instructions.md](../.github/copilot-instructions.md) - File System Access API section
