namespace Karamel.Web.Models;

public record Song
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Artist { get; init; }
    public required string Title { get; init; }
    public required string Mp3FileName { get; init; }
    public required string CdgFileName { get; init; }
    public string? AddedBySinger { get; init; }
}
