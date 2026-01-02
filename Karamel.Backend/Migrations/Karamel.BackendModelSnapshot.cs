using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Karamel.Backend.Migrations
{
    [DbContext(typeof(Karamel.Backend.Data.BackendDbContext))]
    partial class Karamel_BackendModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.1");

            modelBuilder.Entity("Karamel.Backend.Models.Session", b =>
            {
                b.Property<Guid>("Id").ValueGeneratedNever();
                b.Property<string>("LinkToken").IsRequired();
                b.Property<DateTime>("CreatedAt");
                b.Property<DateTime?>("ExpiresAt");
                b.Property<bool>("RequireSingerName");
                b.Property<int>("PauseBetweenSongsSeconds");
                b.HasKey("Id");
                b.ToTable("Sessions");
            });

            modelBuilder.Entity("Karamel.Backend.Models.Playlist", b =>
            {
                b.Property<Guid>("Id").ValueGeneratedNever();
                b.Property<Guid>("SessionId");
                b.HasKey("Id");
                b.ToTable("Playlists");
            });

            modelBuilder.Entity("Karamel.Backend.Models.PlaylistItem", b =>
            {
                b.Property<Guid>("Id").ValueGeneratedNever();
                b.Property<Guid>("PlaylistId");
                b.Property<int>("Position");
                b.Property<string>("Artist").IsRequired();
                b.Property<string>("Title").IsRequired();
                b.Property<string>("SingerName");
                b.HasKey("Id");
                b.HasIndex("PlaylistId");
                b.ToTable("PlaylistItems");
            });

            modelBuilder.Entity("Karamel.Backend.Models.Playlist", b =>
            {
                b.HasMany("Items").WithOne().HasForeignKey("PlaylistId").OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
