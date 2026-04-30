namespace FriendMap.Admin.Models;

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
    AdminEngagementSnapshotDto Engagement,
    AdminFeatureFlagsDto FeatureFlags,
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

public record AdminEngagementSnapshotDto(
    int StoryCommentsLast24h,
    int StoryReactionsLast24h,
    int StorySharesLast24h,
    int VenueRatingsLast24h,
    int FlaggedRatings,
    int FlareResponsesLast24h,
    int FlareRelaysLast24h,
    int TableJoinsLast24h,
    int FeedReentryPending,
    int ActiveAdventures,
    int ActiveObjectives);

public record AdminFeatureFlagsDto(
    bool DemoSignalsEnabled,
    bool TestUsersEnabled);

public record AdminFeatureFlagsUpdateRequest(
    bool? DemoSignalsEnabled,
    bool? TestUsersEnabled);

public record AdminAdventureDto(
    Guid Id,
    string Title,
    string Description,
    string Scope,
    bool IsActive,
    int RewardPoints,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AdminObjectiveDto> Objectives);

public record AdminObjectiveDto(
    Guid Id,
    string Title,
    string MetricKey,
    int Target,
    int RewardPoints,
    bool IsActive);

public record AdminAdventureUpsertRequest(
    string Title,
    string Description,
    string Scope,
    bool IsActive,
    IReadOnlyList<AdminObjectiveUpsertRequest> Objectives);

public record AdminObjectiveUpsertRequest(
    Guid Id,
    string Title,
    string MetricKey,
    int Target,
    int RewardPoints,
    bool IsActive);

public class AdventureEditModel
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Scope { get; set; } = "weekly";
    public bool IsActive { get; set; } = true;
    public List<ObjectiveEditModel> Objectives { get; set; } =
    [
        new ObjectiveEditModel()
    ];

    public AdminAdventureUpsertRequest ToRequest() =>
        new(Title, Description, Scope, IsActive, Objectives.Select(x => x.ToRequest()).ToList());

    public static AdventureEditModel FromDto(AdminAdventureDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        Description = dto.Description,
        Scope = dto.Scope,
        IsActive = dto.IsActive,
        Objectives = dto.Objectives.Select(ObjectiveEditModel.FromDto).ToList()
    };
}

public class ObjectiveEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Visita un locale";
    public string MetricKey { get; set; } = "check_in";
    public int Target { get; set; } = 1;
    public int RewardPoints { get; set; } = 50;
    public bool IsActive { get; set; } = true;

    public AdminObjectiveUpsertRequest ToRequest() =>
        new(Id, Title, MetricKey, Target, RewardPoints, IsActive);

    public static ObjectiveEditModel FromDto(AdminObjectiveDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        MetricKey = dto.MetricKey,
        Target = dto.Target,
        RewardPoints = dto.RewardPoints,
        IsActive = dto.IsActive
    };
}

public record ModerationQueueItemDto(
    Guid ReportId,
    DateTimeOffset CreatedAtUtc,
    string ReasonCode,
    string Status,
    Guid? ReportedUserId,
    Guid? ReportedVenueId,
    Guid? ReportedSocialTableId);

public record AdminVenueDto(
    Guid Id,
    string ExternalProviderId,
    string Name,
    string Category,
    string AddressLine,
    string City,
    string CountryCode,
    string? PhoneNumber,
    string? WebsiteUrl,
    string? HoursSummary,
    string? CoverImageUrl,
    string? Description,
    string? TagsCsv,
    double? Latitude,
    double? Longitude,
    bool IsClaimed,
    string VisibilityStatus);

public class VenueEditModel
{
    public Guid? Id { get; set; }
    public string ExternalProviderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "bar";
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = "Milano";
    public string CountryCode { get; set; } = "IT";
    public string? PhoneNumber { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? HoursSummary { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
    public string? TagsCsv { get; set; }
    public double Latitude { get; set; } = 45.4642;
    public double Longitude { get; set; } = 9.1900;
    public bool IsClaimed { get; set; }
    public string VisibilityStatus { get; set; } = "public";

    public static VenueEditModel FromDto(AdminVenueDto dto)
    {
        return new VenueEditModel
        {
            Id = dto.Id,
            ExternalProviderId = dto.ExternalProviderId,
            Name = dto.Name,
            Category = dto.Category,
            AddressLine = dto.AddressLine,
            City = dto.City,
            CountryCode = dto.CountryCode,
            PhoneNumber = dto.PhoneNumber,
            WebsiteUrl = dto.WebsiteUrl,
            HoursSummary = dto.HoursSummary,
            CoverImageUrl = dto.CoverImageUrl,
            Description = dto.Description,
            TagsCsv = dto.TagsCsv,
            Latitude = dto.Latitude ?? 45.4642,
            Longitude = dto.Longitude ?? 9.1900,
            IsClaimed = dto.IsClaimed,
            VisibilityStatus = dto.VisibilityStatus
        };
    }
}

public class VenueImportModel
{
    public string? Query { get; set; }
    public string? Area { get; set; } = "Tradate";
    public string? CountryCodesCsv { get; set; } = "it";
    public double Latitude { get; set; } = 45.7118;
    public double Longitude { get; set; } = 8.9076;
    public int RadiusMeters { get; set; } = 3000;
    public string? CategoriesCsv { get; set; }
    public int Limit { get; set; } = 250;
    public string VisibilityStatus { get; set; } = "review";
    public bool UpdateExisting { get; set; } = true;
}

public record VenueImportCandidateDto(
    string ExternalProviderId,
    string Name,
    string Category,
    string AddressLine,
    string City,
    string CountryCode,
    string? PhoneNumber,
    string? WebsiteUrl,
    string? HoursSummary,
    string? CoverImageUrl,
    string? Description,
    string? TagsCsv,
    double Latitude,
    double Longitude,
    bool AlreadyExists,
    Guid? ExistingVenueId);

public record VenueImportResultDto(
    int Found,
    int Created,
    int Updated,
    int Skipped,
    IEnumerable<AdminVenueDto> Venues);
