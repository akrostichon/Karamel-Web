namespace Karamel.Backend.Models
{
    public class Session
    {
        public Guid Id { get; set; }
        public string LinkToken { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool RequireSingerName { get; set; }
        public int PauseBetweenSongsSeconds { get; set; }
    }
}
