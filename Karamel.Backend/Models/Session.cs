namespace Karamel.Backend.Models
{
    public class Session
    {
        public Guid Id { get; set; }
        public string AdminToken { get; set; } = string.Empty;     // Full permissions
        public string SingerToken { get; set; } = string.Empty;    // Limited permissions
        public string LinkToken { get; set; } = string.Empty;      // Backward compat (will be AdminToken)
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        
        // NEW: JSON config column (EF Core Owned Entity Type)
        public SessionConfig Config { get; set; } = new();
    }
}
