using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Karamel.Backend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLinkToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve data: copy LinkToken into AdminToken for any existing rows that haven't been migrated yet.
            migrationBuilder.Sql(
                "UPDATE Sessions SET AdminToken = LinkToken WHERE (AdminToken IS NULL OR AdminToken = '') AND LinkToken IS NOT NULL AND LinkToken != ''");

            migrationBuilder.DropColumn(
                name: "LinkToken",
                table: "Sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LinkToken",
                table: "Sessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // Restore data: populate LinkToken from AdminToken
            migrationBuilder.Sql(
                "UPDATE Sessions SET LinkToken = AdminToken");
        }
    }
}
