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
