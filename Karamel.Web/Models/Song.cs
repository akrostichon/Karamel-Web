namespace Karamel.Web.Models;

public enum MediaType
{
    Mp3Cdg = 0,
    Video = 1
}

public enum SongSourceType
{
    Directory,
    Zip
}

public record Song
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Artist { get; init; }
    public required string Title { get; init; }
    public MediaType MediaType { get; init; } = MediaType.Mp3Cdg;
    
    // MP3+CDG properties (required for MediaType.Mp3Cdg)
    public string? Mp3FileName { get; init; }
    public string? CdgFileName { get; init; }
    
    // Video properties (required for MediaType.Video)
    public string? VideoFileName { get; init; }
    public string? VideoExtension { get; init; }
    
    public string? AddedBySinger { get; init; }
    public string? Path { get; init; }
    public string? FullPath { get; init; }
    public SongSourceType SourceType { get; init; } = SongSourceType.Directory;
    
    // ZIP-origin metadata. When SourceType == Zip the Zip* fields
    // describe the origin ZIP file and inner entry paths for MP3 and CDG.
    public string? ZipFilePath { get; init; }
    public string? ZipFileName { get; init; }
    public string? ZipEntryMp3Path { get; init; }
    public string? ZipEntryCdgPath { get; init; }

    /// <summary>
    /// Validates that all required fields are set based on MediaType
    /// </summary>
    public bool IsValid()
    {
        return MediaType switch
        {
            MediaType.Mp3Cdg => !string.IsNullOrEmpty(Mp3FileName) && !string.IsNullOrEmpty(CdgFileName),
            MediaType.Video => !string.IsNullOrEmpty(VideoFileName),
            _ => false
        };
    }

    /// <summary>
    /// Returns the primary file name based on MediaType
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when song is invalid</exception>
    public string GetPrimaryFileName()
    {
        if (!IsValid())
        {
            throw new InvalidOperationException("Cannot get primary file name for invalid song");
        }

        return MediaType switch
        {
            MediaType.Mp3Cdg => Mp3FileName!,
            MediaType.Video => VideoFileName!,
            _ => throw new InvalidOperationException("Unknown media type")
        };
    }
}
