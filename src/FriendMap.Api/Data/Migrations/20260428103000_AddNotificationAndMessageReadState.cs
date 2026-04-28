using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendMap.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260428103000_AddNotificationAndMessageReadState")]
    public partial class AddNotificationAndMessageReadState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE notification_outbox
                ADD COLUMN IF NOT EXISTS is_read boolean NOT NULL DEFAULT FALSE;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE notification_outbox
                ADD COLUMN IF NOT EXISTS read_at_utc timestamp with time zone NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE notification_outbox
                ADD COLUMN IF NOT EXISTS deleted_at_utc timestamp with time zone NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE direct_messages
                ADD COLUMN IF NOT EXISTS read_at_utc timestamp with time zone NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE direct_messages
                SET read_at_utc = created_at_utc
                WHERE read_at_utc IS NULL
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE notification_outbox DROP COLUMN IF EXISTS is_read;");
            migrationBuilder.Sql("ALTER TABLE notification_outbox DROP COLUMN IF EXISTS read_at_utc;");
            migrationBuilder.Sql("ALTER TABLE notification_outbox DROP COLUMN IF EXISTS deleted_at_utc;");
            migrationBuilder.Sql("ALTER TABLE direct_messages DROP COLUMN IF EXISTS read_at_utc;");
        }
    }
}
