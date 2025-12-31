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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Session>(b =>
            {
                b.HasKey(s => s.Id);
                b.Property(s => s.LinkToken).IsRequired();
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
        }
    }
}
