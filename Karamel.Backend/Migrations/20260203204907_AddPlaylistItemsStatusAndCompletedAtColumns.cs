using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Karamel.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSongsTableAndStatusColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Songs table only if it doesn't exist (using raw SQL for idempotency)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Songs]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [Songs] (
                        [Id] uniqueidentifier NOT NULL,
                        [SessionId] uniqueidentifier NOT NULL,
                        [Artist] nvarchar(512) NOT NULL,
                        [Title] nvarchar(512) NOT NULL,
                        [MetadataJson] nvarchar(max) NULL,
                        [AddedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_Songs] PRIMARY KEY ([Id])
                    );
                    
                    CREATE INDEX [IX_Songs_SessionId_AddedAt] ON [Songs] ([SessionId], [AddedAt]);
                    CREATE INDEX [IX_Songs_SessionId_Artist_Title] ON [Songs] ([SessionId], [Artist], [Title]);
                END
            ");

      

            // Add Status column to PlaylistItems
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PlaylistItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Add CompletedAt column to PlaylistItems
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "PlaylistItems",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Songs");

            migrationBuilder.DropColumn(
                name: "SongId",
                table: "PlaylistItems");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PlaylistItems");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "PlaylistItems");
        }
    }
}
