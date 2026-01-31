using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Karamel.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSongIdToPlaylistItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SongId",
                table: "PlaylistItems",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SongId",
                table: "PlaylistItems");
        }
    }
}
