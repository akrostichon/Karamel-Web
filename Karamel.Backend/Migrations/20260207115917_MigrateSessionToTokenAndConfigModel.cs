using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Karamel.Backend.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSessionToTokenAndConfigModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PauseBetweenSongsSeconds",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RequireSingerName",
                table: "Sessions");

            migrationBuilder.AddColumn<string>(
                name: "AdminToken",
                table: "Sessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Config",
                table: "Sessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "SingerToken",
                table: "Sessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminToken",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Config",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SingerToken",
                table: "Sessions");

            migrationBuilder.AddColumn<int>(
                name: "PauseBetweenSongsSeconds",
                table: "Sessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireSingerName",
                table: "Sessions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
