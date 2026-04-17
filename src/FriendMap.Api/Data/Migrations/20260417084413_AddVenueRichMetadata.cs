using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendMap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueRichMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover_image_url",
                table: "venues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "venues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tags_csv",
                table: "venues",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_image_url",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "description",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "tags_csv",
                table: "venues");
        }
    }
}
