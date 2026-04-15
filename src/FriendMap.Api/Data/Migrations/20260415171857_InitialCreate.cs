using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace FriendMap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nickname = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    birth_year = table.Column<int>(type: "integer", nullable: true),
                    gender = table.Column<string>(type: "text", nullable: false),
                    is_ghost_mode_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    share_presence_with_friends = table.Column<bool>(type: "boolean", nullable: false),
                    share_intentions_with_friends = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_provider_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    address_line = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<Point>(type: "geography (point, 4326)", nullable: true),
                    is_claimed = table.Column<bool>(type: "boolean", nullable: false),
                    visibility_status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venues", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "friend_relations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    addressee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friend_relations", x => x.id);
                    table.ForeignKey(
                        name: "fk_friend_relations_app_users_addressee_id",
                        column: x => x.addressee_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friend_relations_app_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_device_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "text", nullable: false),
                    device_token = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_device_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_device_tokens_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_interests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_interests", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_interests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "social_tables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    join_policy = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_social_tables", x => x.id);
                    table.ForeignKey(
                        name: "fk_social_tables_app_users_host_user_id",
                        column: x => x.host_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_social_tables_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venue_affluence_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bucket_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bucket_end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    active_users_estimated = table.Column<int>(type: "integer", nullable: false),
                    density_level = table.Column<string>(type: "text", nullable: false),
                    aggregated_age_json = table.Column<string>(type: "text", nullable: true),
                    aggregated_gender_json = table.Column<string>(type: "text", nullable: true),
                    is_suppressed_for_privacy = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venue_affluence_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_venue_affluence_snapshots_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venue_checkins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_manual = table.Column<bool>(type: "boolean", nullable: false),
                    visibility_level = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venue_checkins", x => x.id);
                    table.ForeignKey(
                        name: "fk_venue_checkins_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_venue_checkins_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venue_intentions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    visibility_level = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venue_intentions", x => x.id);
                    table.ForeignKey(
                        name: "fk_venue_intentions_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_venue_intentions_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moderation_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reported_venue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reported_social_table_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_moderation_reports_app_users_reported_user_id",
                        column: x => x.reported_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_moderation_reports_app_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_moderation_reports_social_tables_reported_social_table_id",
                        column: x => x.reported_social_table_id,
                        principalTable: "social_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_moderation_reports_venues_reported_venue_id",
                        column: x => x.reported_venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "social_table_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    social_table_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_social_table_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_social_table_participants_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_social_table_participants_social_tables_social_table_id",
                        column: x => x.social_table_id,
                        principalTable: "social_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_friend_relations_addressee_id",
                table: "friend_relations",
                column: "addressee_id");

            migrationBuilder.CreateIndex(
                name: "ix_friend_relations_requester_id_addressee_id",
                table: "friend_relations",
                columns: new[] { "requester_id", "addressee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_reports_reported_social_table_id",
                table: "moderation_reports",
                column: "reported_social_table_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_reports_reported_user_id",
                table: "moderation_reports",
                column: "reported_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_reports_reported_venue_id",
                table: "moderation_reports",
                column: "reported_venue_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_reports_reporter_user_id",
                table: "moderation_reports",
                column: "reporter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_device_tokens_user_id_platform_device_token",
                table: "notification_device_tokens",
                columns: new[] { "user_id", "platform", "device_token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_social_table_participants_social_table_id_user_id",
                table: "social_table_participants",
                columns: new[] { "social_table_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_social_table_participants_user_id",
                table: "social_table_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_social_tables_host_user_id",
                table: "social_tables",
                column: "host_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_social_tables_venue_id",
                table: "social_tables",
                column: "venue_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_interests_user_id_tag",
                table: "user_interests",
                columns: new[] { "user_id", "tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_venue_affluence_snapshots_venue_id_bucket_start_utc",
                table: "venue_affluence_snapshots",
                columns: new[] { "venue_id", "bucket_start_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_venue_checkins_user_id",
                table: "venue_checkins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_venue_checkins_venue_id_expires_at_utc",
                table: "venue_checkins",
                columns: new[] { "venue_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_venue_intentions_user_id",
                table: "venue_intentions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_venue_intentions_venue_id_starts_at_utc_ends_at_utc",
                table: "venue_intentions",
                columns: new[] { "venue_id", "starts_at_utc", "ends_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_venues_external_provider_id",
                table: "venues",
                column: "external_provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_venues_location",
                table: "venues",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "GIST");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "friend_relations");

            migrationBuilder.DropTable(
                name: "moderation_reports");

            migrationBuilder.DropTable(
                name: "notification_device_tokens");

            migrationBuilder.DropTable(
                name: "social_table_participants");

            migrationBuilder.DropTable(
                name: "user_interests");

            migrationBuilder.DropTable(
                name: "venue_affluence_snapshots");

            migrationBuilder.DropTable(
                name: "venue_checkins");

            migrationBuilder.DropTable(
                name: "venue_intentions");

            migrationBuilder.DropTable(
                name: "social_tables");

            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "venues");
        }
    }
}
