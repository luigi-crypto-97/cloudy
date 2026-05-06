using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendMap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppleAuthToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "apple_subject",
                table: "app_users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_users_apple_subject",
                table: "app_users",
                column: "apple_subject",
                unique: true,
                filter: "apple_subject IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_app_users_apple_subject",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "apple_subject",
                table: "app_users");
        }
    }
}
