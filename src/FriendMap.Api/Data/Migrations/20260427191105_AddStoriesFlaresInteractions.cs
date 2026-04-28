using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendMap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoriesFlaresInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "venue_id",
                table: "user_stories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "flare_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flare_signals", x => x.id);
                    table.ForeignKey(
                        name: "fk_flare_signals_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_story_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_story_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_story_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_story_comments_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_story_comments_user_stories_user_story_id",
                        column: x => x.user_story_id,
                        principalTable: "user_stories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_story_reactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_story_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reaction_type = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_story_reactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_story_reactions_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_story_reactions_user_stories_user_story_id",
                        column: x => x.user_story_id,
                        principalTable: "user_stories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flare_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flare_signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flare_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_flare_responses_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_flare_responses_flare_signals_flare_signal_id",
                        column: x => x.flare_signal_id,
                        principalTable: "flare_signals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_stories_venue_id_expires_at_utc",
                table: "user_stories",
                columns: new[] { "venue_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_flare_responses_flare_signal_id_user_id",
                table: "flare_responses",
                columns: new[] { "flare_signal_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_flare_responses_user_id",
                table: "flare_responses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_flare_signals_expires_at_utc",
                table: "flare_signals",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_flare_signals_user_id",
                table: "flare_signals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_comments_user_id",
                table: "user_story_comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_comments_user_story_id_created_at_utc",
                table: "user_story_comments",
                columns: new[] { "user_story_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_user_story_reactions_user_id",
                table: "user_story_reactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_reactions_user_story_id_user_id_reaction_type",
                table: "user_story_reactions",
                columns: new[] { "user_story_id", "user_id", "reaction_type" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_user_stories_venues_venue_id",
                table: "user_stories",
                column: "venue_id",
                principalTable: "venues",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_stories_venues_venue_id",
                table: "user_stories");

            migrationBuilder.DropTable(
                name: "flare_responses");

            migrationBuilder.DropTable(
                name: "user_story_comments");

            migrationBuilder.DropTable(
                name: "user_story_reactions");

            migrationBuilder.DropTable(
                name: "flare_signals");

            migrationBuilder.DropIndex(
                name: "ix_user_stories_venue_id_expires_at_utc",
                table: "user_stories");

            migrationBuilder.DropColumn(
                name: "venue_id",
                table: "user_stories");
        }
    }
}
