namespace Karamel.Backend.Contracts
{
    public record PlaylistItemDto(string Id, string Artist, string Title, string AddedBy);

    public record PlaylistDto(string SessionId, PlaylistItemDto[] Items);
}
