using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendMap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueContactMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "hours_summary",
                table: "venues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "venues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website_url",
                table: "venues",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hours_summary",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "website_url",
                table: "venues");
        }
    }
}
