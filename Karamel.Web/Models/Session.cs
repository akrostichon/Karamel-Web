namespace Karamel.Web.Models;

public record Session
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required string LibraryPath { get; init; }
    public bool RequireSingerName { get; init; } = true;
    public bool AllowSingersToReorder { get; init; } = false;
    public bool PauseBetweenSongs { get; init; } = true;
    public int PauseBetweenSongsSeconds { get; init; } = 5;
    public string FilenamePattern { get; init; } = "%artist - %title";
}
