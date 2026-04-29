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
