namespace Karamel.Web.Models;

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
    public required string Mp3FileName { get; init; }
    public required string CdgFileName { get; init; }
    public string? AddedBySinger { get; init; }
    public string? Path { get; init; }
    public string? FullPath { get; init; }
    public string? ZipFilePath { get; init; }

    // New ZIP-origin metadata. When SourceType == Zip the Zip* fields
    // describe the origin ZIP file and inner entry paths for MP3 and CDG.
    public SongSourceType SourceType { get; init; } = SongSourceType.Directory;
    public string? ZipFileName { get; init; }
    public string? ZipEntryMp3Path { get; init; }
    public string? ZipEntryCdgPath { get; init; }
}
