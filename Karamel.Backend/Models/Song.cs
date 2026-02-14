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

        /// <summary>
        /// Optional JSON metadata for the song.
        /// For videos, contains: {"mediaType": "video", "extension": ".mp4", "durationSeconds": 180, "width": 1920, "height": 1080}
        /// For MP3+CDG songs, may contain: {"durationSeconds": 180} or other metadata
        /// </summary>
        public string? MetadataJson { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
