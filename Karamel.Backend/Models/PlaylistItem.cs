namespace Karamel.Backend.Models
{
    public enum SongStatus
    {
        Queued = 0,
        UpNext = 1,
        NowPlaying = 2,
        Completed = 3
    }

    public class PlaylistItem
    {
        public Guid Id { get; set; }
        public Guid PlaylistId { get; set; }
        public int Position { get; set; }
        public string Artist { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? SingerName { get; set; }
        public Guid? SongId { get; set; }  // FK to Songs table for enrichment
        public SongStatus Status { get; set; } = SongStatus.Queued;
        public DateTime? CompletedAt { get; set; }
    }
}
