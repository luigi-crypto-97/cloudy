namespace FriendMap.Api.Contracts;

public record ModerationQueueItemDto(
    Guid ReportId,
    DateTimeOffset CreatedAtUtc,
    string ReasonCode,
    string Status,
    Guid? ReportedUserId,
    Guid? ReportedVenueId,
    Guid? ReportedSocialTableId);

public record DashboardOverviewDto(
    int ActiveUsers24h,
    int CheckInsActive,
    int IntentionsActive,
    int OpenReports,
    int ActiveTables);

public record AdminUserMonitorDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string Status,
    int FriendsCount,
    bool IsGhostModeEnabled,
    bool SharePresenceWithFriends,
    bool ShareIntentionsWithFriends,
    string PresenceState,
    string? VenueName,
    string? VenueCategory,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastSignalAtUtc,
    string PrivacyLevel);

public record AdminMonitorSnapshotDto(
    DateTimeOffset GeneratedAtUtc,
    AdminKpiDto Kpi,
    AdminSystemHealthDto Health,
    AdminPrivacySnapshotDto Privacy,
    IEnumerable<AdminVenuePulseDto> VenuePulses,
    IEnumerable<AdminTimelineEventDto> Timeline,
    IEnumerable<AdminUserMonitorDto> Users);

public record AdminKpiDto(
    int TotalUsers,
    int ActiveUsers,
    int NewUsers24h,
    int CheckInsActive,
    int IntentionsActive,
    int ActiveStories,
    int ActiveFlares,
    int ActiveTables,
    int MessagesLastHour,
    int OpenReports,
    int PendingNotifications,
    int FailedNotifications);

public record AdminSystemHealthDto(
    string ApiStatus,
    long DatabaseLatencyMs,
    string MediaStorageProvider,
    string MediaStorageVisibility,
    int NotificationBacklog,
    int FailedNotificationBacklog,
    DateTimeOffset CheckedAtUtc);

public record AdminPrivacySnapshotDto(
    int GhostModeUsers,
    int PresenceOptOutUsers,
    int IntentionOptOutUsers,
    int VenueLevelVisibleUsers,
    string LocationPrecision);

public record AdminVenuePulseDto(
    Guid VenueId,
    string Name,
    string Category,
    string City,
    double? Latitude,
    double? Longitude,
    int CheckedInCount,
    int IntentionCount,
    int EnergyScore,
    DateTimeOffset? LastSignalAtUtc);

public record AdminTimelineEventDto(
    string Kind,
    string Title,
    string Subtitle,
    DateTimeOffset CreatedAtUtc,
    string Severity);
