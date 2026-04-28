using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendMap.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260428131500_AddGroupVenueChats")]
    public partial class AddGroupVenueChats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "group_chats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    last_message_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_chats", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_chats_app_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_chats_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_chat_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    group_chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    last_read_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_chat_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_chat_members_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_chat_members_group_chats_group_chat_id",
                        column: x => x.group_chat_id,
                        principalTable: "group_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    group_chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_chat_messages_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_chat_messages_group_chats_group_chat_id",
                        column: x => x.group_chat_id,
                        principalTable: "group_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_group_chat_members_group_chat_id_user_id",
                table: "group_chat_members",
                columns: new[] { "group_chat_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_chat_members_user_id",
                table: "group_chat_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_chat_messages_group_chat_id_created_at_utc",
                table: "group_chat_messages",
                columns: new[] { "group_chat_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_group_chat_messages_user_id",
                table: "group_chat_messages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_chats_created_by_user_id",
                table: "group_chats",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_chats_kind_venue_id",
                table: "group_chats",
                columns: new[] { "kind", "venue_id" });

            migrationBuilder.CreateIndex(
                name: "ix_group_chats_last_message_at_utc",
                table: "group_chats",
                column: "last_message_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_group_chats_venue_id",
                table: "group_chats",
                column: "venue_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "group_chat_messages");
            migrationBuilder.DropTable(name: "group_chat_members");
            migrationBuilder.DropTable(name: "group_chats");
        }
    }
}
