namespace Karamel.Backend.Models
{
    public class Playlist
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public List<PlaylistItem> Items { get; set; } = new();
    }
}
