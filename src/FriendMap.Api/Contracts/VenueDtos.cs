namespace FriendMap.Api.Contracts;

public record VenueMapMarkerDto(
    Guid VenueId,
    string Name,
    string Category,
    double Latitude,
    double Longitude,
    int PeopleEstimate,
    string DensityLevel,
    int BubbleIntensity,
    bool DemographicDataAvailable);

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
