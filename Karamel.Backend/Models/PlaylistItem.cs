namespace Karamel.Backend.Models
{
    public class PlaylistItem
    {
        public Guid Id { get; set; }
        public Guid PlaylistId { get; set; }
        public int Position { get; set; }
        public string Artist { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? SingerName { get; set; }
    }
}
