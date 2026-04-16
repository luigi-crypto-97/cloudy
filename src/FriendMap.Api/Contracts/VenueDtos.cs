namespace FriendMap.Api.Contracts;

public record GeoPointDto(
    double Latitude,
    double Longitude);

public record VenueMapLayerDto(
    IEnumerable<VenueMapMarkerDto> Markers,
    IEnumerable<VenueMapAreaDto> Areas);

public record VenueMapMarkerDto(
    Guid VenueId,
    string Name,
    string Category,
    string AddressLine,
    string City,
    double Latitude,
    double Longitude,
    bool IsOpenNow,
    int PeopleEstimate,
    string DensityLevel,
    int BubbleIntensity,
    bool DemographicDataAvailable,
    int ActiveCheckIns,
    int ActiveIntentions,
    int OpenTables,
    IEnumerable<PresencePreviewDto> PresencePreview);

public record VenueMapAreaDto(
    string AreaId,
    string Label,
    double CentroidLatitude,
    double CentroidLongitude,
    int PeopleCount,
    string DensityLevel,
    int BubbleIntensity,
    int VenueCount,
    int ActiveCheckIns,
    int ActiveIntentions,
    int OpenTables,
    int PresenceCount,
    IEnumerable<Guid> VenueIds,
    IEnumerable<GeoPointDto> Polygon,
    IEnumerable<PresencePreviewDto> PresencePreview);

public record PresencePreviewDto(
    Guid UserId,
    string DisplayName,
    string Nickname,
    string? AvatarUrl);

public record VenueDetailsDto(
    Guid VenueId,
    string Name,
    string Category,
    string AddressLine,
    string City,
    string DensityLevel,
    int PeopleEstimate,
    bool DemographicDataAvailable,
    object? AgeDistribution,
    object? GenderDistribution,
    IEnumerable<SocialTableDto> UpcomingTables,
    IEnumerable<IntentionCountDto> IntentionWindows);

public record AdminVenueDto(
    Guid Id,
    string ExternalProviderId,
    string Name,
    string Category,
    string AddressLine,
    string City,
    string CountryCode,
    double? Latitude,
    double? Longitude,
    bool IsClaimed,
    string VisibilityStatus);

public record UpsertVenueRequest(
    string ExternalProviderId,
    string Name,
    string Category,
    string AddressLine,
    string City,
    string CountryCode,
    double Latitude,
    double Longitude,
    bool IsClaimed,
    string VisibilityStatus);

public record SocialTableDto(
    Guid Id,
    string Title,
    DateTimeOffset StartsAtUtc,
    int Capacity,
    int Requested,
    int Accepted,
    string JoinPolicy,
    string Status);

public record IntentionCountDto(
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    int Count);

public record CreateIntentionRequest(
    Guid UserId,
    Guid VenueId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Note);

public record CreateCheckInRequest(
    Guid UserId,
    Guid VenueId,
    int TtlMinutes);

public record CreateSocialTableRequest(
    Guid HostUserId,
    Guid VenueId,
    string Title,
    string? Description,
    DateTimeOffset StartsAtUtc,
    int Capacity,
    string JoinPolicy);
