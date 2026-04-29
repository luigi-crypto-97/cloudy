using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendMap.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260429172000_AddFeedDeepLinksAndFatigue")]
    public partial class AddFeedDeepLinksAndFatigue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS deep_link_tokens (
                    id uuid PRIMARY KEY,
                    created_at_utc timestamp with time zone NOT NULL,
                    updated_at_utc timestamp with time zone NULL,
                    token text NOT NULL,
                    link_type text NOT NULL,
                    target_id uuid NOT NULL,
                    created_by_user_id uuid NULL,
                    expires_at_utc timestamp with time zone NOT NULL,
                    max_uses integer NOT NULL DEFAULT 30,
                    use_count integer NOT NULL DEFAULT 0,
                    revoked_at_utc timestamp with time zone NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_deep_link_tokens_token
                ON deep_link_tokens (token);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_deep_link_tokens_link_type_target_id_expires_at_utc
                ON deep_link_tokens (link_type, target_id, expires_at_utc);
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS feed_card_fatigues (
                    id uuid PRIMARY KEY,
                    created_at_utc timestamp with time zone NOT NULL,
                    updated_at_utc timestamp with time zone NULL,
                    user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
                    card_key text NOT NULL,
                    seen_count integer NOT NULL DEFAULT 0,
                    dismissed_count integer NOT NULL DEFAULT 0,
                    last_seen_at_utc timestamp with time zone NULL,
                    last_dismissed_at_utc timestamp with time zone NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_feed_card_fatigues_user_id_card_key
                ON feed_card_fatigues (user_id, card_key);
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS flare_relay_audits (
                    id uuid PRIMARY KEY,
                    created_at_utc timestamp with time zone NOT NULL,
                    updated_at_utc timestamp with time zone NULL,
                    flare_signal_id uuid NOT NULL REFERENCES flare_signals(id) ON DELETE CASCADE,
                    sender_user_id uuid NOT NULL,
                    target_user_id uuid NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_flare_relay_audits_sender_user_id_created_at_utc
                ON flare_relay_audits (sender_user_id, created_at_utc);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_flare_relay_audits_flare_signal_id_sender_user_id_target_user_id
                ON flare_relay_audits (flare_signal_id, sender_user_id, target_user_id);
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS feed_reentry_notification_states (
                    id uuid PRIMARY KEY,
                    created_at_utc timestamp with time zone NOT NULL,
                    updated_at_utc timestamp with time zone NULL,
                    user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
                    trigger_type text NOT NULL,
                    trigger_key text NOT NULL,
                    last_sent_at_utc timestamp with time zone NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_feed_reentry_notification_states_user_id_trigger_type_trigger_key
                ON feed_reentry_notification_states (user_id, trigger_type, trigger_key);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS feed_reentry_notification_states;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS flare_relay_audits;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS feed_card_fatigues;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS deep_link_tokens;");
        }
    }
}
