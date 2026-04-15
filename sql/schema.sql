CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE app_users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    nickname TEXT NOT NULL,
    display_name TEXT NULL,
    avatar_url TEXT NULL,
    birth_year INT NULL,
    gender TEXT NOT NULL DEFAULT 'undisclosed',
    is_ghost_mode_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    share_presence_with_friends BOOLEAN NOT NULL DEFAULT TRUE,
    share_intentions_with_friends BOOLEAN NOT NULL DEFAULT TRUE,
    status TEXT NOT NULL DEFAULT 'active',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE venues (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    external_provider_id TEXT NOT NULL,
    name TEXT NOT NULL,
    category TEXT NOT NULL DEFAULT 'bar',
    address_line TEXT NOT NULL,
    city TEXT NOT NULL,
    country_code TEXT NOT NULL DEFAULT 'IT',
    location GEOGRAPHY(POINT,4326) NULL,
    is_claimed BOOLEAN NOT NULL DEFAULT FALSE,
    visibility_status TEXT NOT NULL DEFAULT 'public',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE INDEX idx_venues_external_provider_id ON venues(external_provider_id);
CREATE INDEX idx_venues_location ON venues USING GIST(location);

CREATE TABLE user_interests (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    tag TEXT NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL,
    UNIQUE(user_id, tag)
);

CREATE TABLE friend_relations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    requester_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    addressee_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    status TEXT NOT NULL DEFAULT 'pending',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL,
    UNIQUE(requester_id, addressee_id)
);

CREATE TABLE venue_intentions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    venue_id UUID NOT NULL REFERENCES venues(id) ON DELETE CASCADE,
    starts_at_utc TIMESTAMPTZ NOT NULL,
    ends_at_utc TIMESTAMPTZ NOT NULL,
    note TEXT NULL,
    visibility_level TEXT NOT NULL DEFAULT 'friends_or_aggregate',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE INDEX idx_venue_intentions_window ON venue_intentions(venue_id, starts_at_utc, ends_at_utc);

CREATE TABLE venue_checkins (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    venue_id UUID NOT NULL REFERENCES venues(id) ON DELETE CASCADE,
    expires_at_utc TIMESTAMPTZ NOT NULL,
    is_manual BOOLEAN NOT NULL DEFAULT TRUE,
    visibility_level TEXT NOT NULL DEFAULT 'friends_or_aggregate',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE INDEX idx_venue_checkins_active ON venue_checkins(venue_id, expires_at_utc);

CREATE TABLE social_tables (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    venue_id UUID NOT NULL REFERENCES venues(id) ON DELETE CASCADE,
    host_user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    description TEXT NULL,
    starts_at_utc TIMESTAMPTZ NOT NULL,
    capacity INT NOT NULL DEFAULT 6,
    join_policy TEXT NOT NULL DEFAULT 'approval',
    status TEXT NOT NULL DEFAULT 'open',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE social_table_participants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    social_table_id UUID NOT NULL REFERENCES social_tables(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    status TEXT NOT NULL DEFAULT 'requested',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE moderation_reports (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reporter_user_id UUID NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    reported_user_id UUID NULL REFERENCES app_users(id) ON DELETE SET NULL,
    reported_venue_id UUID NULL REFERENCES venues(id) ON DELETE SET NULL,
    reported_social_table_id UUID NULL REFERENCES social_tables(id) ON DELETE SET NULL,
    reason_code TEXT NOT NULL,
    details TEXT NULL,
    status TEXT NOT NULL DEFAULT 'open',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL
);

CREATE TABLE venue_affluence_snapshots (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    venue_id UUID NOT NULL REFERENCES venues(id) ON DELETE CASCADE,
    bucket_start_utc TIMESTAMPTZ NOT NULL,
    bucket_end_utc TIMESTAMPTZ NOT NULL,
    active_users_estimated INT NOT NULL DEFAULT 0,
    density_level TEXT NOT NULL DEFAULT 'low',
    aggregated_age_json JSONB NULL,
    aggregated_gender_json JSONB NULL,
    is_suppressed_for_privacy BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NULL,
    UNIQUE(venue_id, bucket_start_utc)
);
