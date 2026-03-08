using Microsoft.EntityFrameworkCore;
using Karamel.Backend.Models;

namespace Karamel.Backend.Data
{
    public class BackendDbContext : DbContext
    {
        public BackendDbContext(DbContextOptions<BackendDbContext> options) : base(options)
        {
        }

        public DbSet<Session> Sessions { get; set; } = null!;
        public DbSet<Playlist> Playlists { get; set; } = null!;
        public DbSet<PlaylistItem> PlaylistItems { get; set; } = null!;
        public DbSet<Song> Songs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Session>(b =>
            {
                b.HasKey(s => s.Id);
                
                // NEW: Configure SessionConfig as JSON column (EF Core 7.0+)
                b.OwnsOne(s => s.Config, ownedNavigationBuilder =>
                {
                    ownedNavigationBuilder.ToJson();
                });
            });

            modelBuilder.Entity<Playlist>(b =>
            {
                b.HasKey(p => p.Id);
                b.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.PlaylistId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PlaylistItem>(b =>
            {
                b.HasKey(i => i.Id);
                b.Property(i => i.Position).IsRequired();
                b.Property(i => i.Artist).IsRequired();
                b.Property(i => i.Title).IsRequired();
            });

            modelBuilder.Entity<Song>(b =>
            {
                b.HasKey(s => s.Id);
                b.Property(s => s.Artist).IsRequired().HasMaxLength(512);
                b.Property(s => s.Title).IsRequired().HasMaxLength(512);
                b.HasIndex(s => new { s.SessionId, s.AddedAt });
                b.HasIndex(s => new { s.SessionId, s.Artist, s.Title });
            });
        }
    }
}
