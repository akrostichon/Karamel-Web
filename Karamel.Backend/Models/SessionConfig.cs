namespace Karamel.Backend.Models
{
    public class SessionConfig
    {
        public bool RequireSingerName { get; set; } = false;
        public int PauseBetweenSongsSeconds { get; set; } = 5;
        public bool AllowSingersToReorder { get; set; } = false;
        public PlaybackMode PlaybackMode { get; set; } = PlaybackMode.Normal;
    }
}
