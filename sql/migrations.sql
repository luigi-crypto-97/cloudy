CREATE EXTENSION IF NOT EXISTS postgis;
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE EXTENSION IF NOT EXISTS postgis;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE app_users (
        id uuid NOT NULL,
        nickname text NOT NULL,
        display_name text,
        avatar_url text,
        birth_year integer,
        gender text NOT NULL,
        is_ghost_mode_enabled boolean NOT NULL,
        share_presence_with_friends boolean NOT NULL,
        share_intentions_with_friends boolean NOT NULL,
        status text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_app_users PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE venues (
        id uuid NOT NULL,
        external_provider_id text NOT NULL,
        name text NOT NULL,
        category text NOT NULL,
        address_line text NOT NULL,
        city text NOT NULL,
        country_code text NOT NULL,
        location geography (point, 4326),
        is_claimed boolean NOT NULL,
        visibility_status text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_venues PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE friend_relations (
        id uuid NOT NULL,
        requester_id uuid NOT NULL,
        addressee_id uuid NOT NULL,
        status text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_friend_relations PRIMARY KEY (id),
        CONSTRAINT fk_friend_relations_app_users_addressee_id FOREIGN KEY (addressee_id) REFERENCES app_users (id) ON DELETE CASCADE,
        CONSTRAINT fk_friend_relations_app_users_requester_id FOREIGN KEY (requester_id) REFERENCES app_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE notification_device_tokens (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        platform text NOT NULL,
        device_token text NOT NULL,
        is_active boolean NOT NULL,
        last_seen_at_utc timestamp with time zone NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_notification_device_tokens PRIMARY KEY (id),
        CONSTRAINT fk_notification_device_tokens_app_users_user_id FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE user_interests (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        tag text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_user_interests PRIMARY KEY (id),
        CONSTRAINT fk_user_interests_users_user_id FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE social_tables (
        id uuid NOT NULL,
        venue_id uuid NOT NULL,
        host_user_id uuid NOT NULL,
        title text NOT NULL,
        description text,
        starts_at_utc timestamp with time zone NOT NULL,
        capacity integer NOT NULL,
        join_policy text NOT NULL,
        status text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_social_tables PRIMARY KEY (id),
        CONSTRAINT fk_social_tables_app_users_host_user_id FOREIGN KEY (host_user_id) REFERENCES app_users (id) ON DELETE CASCADE,
        CONSTRAINT fk_social_tables_venues_venue_id FOREIGN KEY (venue_id) REFERENCES venues (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE venue_affluence_snapshots (
        id uuid NOT NULL,
        venue_id uuid NOT NULL,
        bucket_start_utc timestamp with time zone NOT NULL,
        bucket_end_utc timestamp with time zone NOT NULL,
        active_users_estimated integer NOT NULL,
        density_level text NOT NULL,
        aggregated_age_json text,
        aggregated_gender_json text,
        is_suppressed_for_privacy boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_venue_affluence_snapshots PRIMARY KEY (id),
        CONSTRAINT fk_venue_affluence_snapshots_venues_venue_id FOREIGN KEY (venue_id) REFERENCES venues (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE venue_checkins (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        venue_id uuid NOT NULL,
        expires_at_utc timestamp with time zone NOT NULL,
        is_manual boolean NOT NULL,
        visibility_level text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_venue_checkins PRIMARY KEY (id),
        CONSTRAINT fk_venue_checkins_app_users_user_id FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE,
        CONSTRAINT fk_venue_checkins_venues_venue_id FOREIGN KEY (venue_id) REFERENCES venues (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE venue_intentions (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        venue_id uuid NOT NULL,
        starts_at_utc timestamp with time zone NOT NULL,
        ends_at_utc timestamp with time zone NOT NULL,
        note text,
        visibility_level text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_venue_intentions PRIMARY KEY (id),
        CONSTRAINT fk_venue_intentions_app_users_user_id FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE,
        CONSTRAINT fk_venue_intentions_venues_venue_id FOREIGN KEY (venue_id) REFERENCES venues (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE moderation_reports (
        id uuid NOT NULL,
        reporter_user_id uuid NOT NULL,
        reported_user_id uuid,
        reported_venue_id uuid,
        reported_social_table_id uuid,
        reason_code text NOT NULL,
        details text,
        status text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_moderation_reports PRIMARY KEY (id),
        CONSTRAINT fk_moderation_reports_app_users_reported_user_id FOREIGN KEY (reported_user_id) REFERENCES app_users (id) ON DELETE SET NULL,
        CONSTRAINT fk_moderation_reports_app_users_reporter_user_id FOREIGN KEY (reporter_user_id) REFERENCES app_users (id) ON DELETE CASCADE,
        CONSTRAINT fk_moderation_reports_social_tables_reported_social_table_id FOREIGN KEY (reported_social_table_id) REFERENCES social_tables (id) ON DELETE SET NULL,
        CONSTRAINT fk_moderation_reports_venues_reported_venue_id FOREIGN KEY (reported_venue_id) REFERENCES venues (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE TABLE social_table_participants (
        id uuid NOT NULL,
        social_table_id uuid NOT NULL,
        user_id uuid NOT NULL,
        status text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_social_table_participants PRIMARY KEY (id),
        CONSTRAINT fk_social_table_participants_app_users_user_id FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE,
        CONSTRAINT fk_social_table_participants_social_tables_social_table_id FOREIGN KEY (social_table_id) REFERENCES social_tables (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_friend_relations_addressee_id ON friend_relations (addressee_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_friend_relations_requester_id_addressee_id ON friend_relations (requester_id, addressee_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_moderation_reports_reported_social_table_id ON moderation_reports (reported_social_table_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_moderation_reports_reported_user_id ON moderation_reports (reported_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_moderation_reports_reported_venue_id ON moderation_reports (reported_venue_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_moderation_reports_reporter_user_id ON moderation_reports (reporter_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_notification_device_tokens_user_id_platform_device_token ON notification_device_tokens (user_id, platform, device_token);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_social_table_participants_social_table_id_user_id ON social_table_participants (social_table_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_social_table_participants_user_id ON social_table_participants (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_social_tables_host_user_id ON social_tables (host_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_social_tables_venue_id ON social_tables (venue_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_user_interests_user_id_tag ON user_interests (user_id, tag);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_venue_affluence_snapshots_venue_id_bucket_start_utc ON venue_affluence_snapshots (venue_id, bucket_start_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_venue_checkins_user_id ON venue_checkins (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_venue_checkins_venue_id_expires_at_utc ON venue_checkins (venue_id, expires_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_venue_intentions_user_id ON venue_intentions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_venue_intentions_venue_id_starts_at_utc_ends_at_utc ON venue_intentions (venue_id, starts_at_utc, ends_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_venues_external_provider_id ON venues (external_provider_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    CREATE INDEX ix_venues_location ON venues USING GIST (location);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415171857_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260415171857_InitialCreate', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415180223_AddNotificationOutbox') THEN
    CREATE TABLE notification_outbox (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        title text NOT NULL,
        body text NOT NULL,
        payload_json text,
        status text NOT NULL,
        attempts integer NOT NULL,
        next_attempt_at_utc timestamp with time zone,
        sent_at_utc timestamp with time zone,
        last_error text,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone,
        CONSTRAINT pk_notification_outbox PRIMARY KEY (id),
        CONSTRAINT fk_notification_outbox_app_users_user_id FOREIGN KEY (user_id) REFERENCES app_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415180223_AddNotificationOutbox') THEN
    CREATE INDEX ix_notification_outbox_status_next_attempt_at_utc ON notification_outbox (status, next_attempt_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415180223_AddNotificationOutbox') THEN
    CREATE INDEX ix_notification_outbox_user_id ON notification_outbox (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260415180223_AddNotificationOutbox') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260415180223_AddNotificationOutbox', '8.0.8');
    END IF;
END $EF$;
COMMIT;

