namespace FriendMap.Api.Contracts;

public record FeedResponseDto(
    IEnumerable<FeedServerItemDto> Items,
    IEnumerable<VenueMapMarkerDto> Venues,
    IEnumerable<FlareDto> Flares,
    IEnumerable<SocialTableSummaryDto> Tables,
    IEnumerable<FeedCardFatigueDto> Fatigue,
    DateTimeOffset GeneratedAtUtc);

public record FeedServerItemDto(
    string Id,
    string Kind,
    double Score,
    DateTimeOffset Timestamp,
    Guid? VenueId,
    Guid? EntityId,
    string Title,
    string Subtitle,
    string PrivacyExplanation,
    string Source,
    string? DeepLink,
    string? ShareUrl);

public record FeedCardFatigueDto(
    string CardKey,
    int SeenCount,
    int DismissedCount,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset? LastDismissedAtUtc);

public record FeedFatigueUpdateRequest(
    string CardKey,
    bool Dismissed);

public record SignedDeepLinkRequest(
    string Type,
    Guid TargetId,
    int? ExpiresInMinutes,
    int? MaxUses);

public record SignedDeepLinkDto(
    string Url,
    DateTimeOffset ExpiresAtUtc);
