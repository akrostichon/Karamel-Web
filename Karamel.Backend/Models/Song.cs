using System.ComponentModel.DataAnnotations;

namespace Karamel.Backend.Models
{
    public class Song
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid SessionId { get; set; }

        [Required]
        [MaxLength(512)]
        public string Artist { get; set; } = string.Empty;

        [Required]
        [MaxLength(512)]
        public string Title { get; set; } = string.Empty;

        public string? MetadataJson { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
