namespace FriendMap.Api.Contracts;

public record SocialConnectionDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string RelationshipStatus,
    int MutualFriendsCount,
    string PresenceState,
    string StatusLabel,
    string? CurrentVenueName,
    string? CurrentVenueCategory);

public record SocialTableInviteDto(
    Guid TableId,
    string Title,
    DateTimeOffset StartsAtUtc,
    string VenueName,
    string VenueCategory,
    Guid HostUserId,
    string HostNickname,
    string? HostDisplayName,
    string? HostAvatarUrl);

public record SocialHubDto(
    IEnumerable<SocialConnectionDto> Friends,
    IEnumerable<SocialConnectionDto> IncomingRequests,
    IEnumerable<SocialConnectionDto> OutgoingRequests,
    IEnumerable<SocialTableInviteDto> TableInvites);

public record SocialMeStateDto(
    bool IsGhostModeEnabled,
    bool SharePresenceWithFriends,
    bool ShareIntentionsWithFriends,
    Guid? ActiveCheckInVenueId,
    string? ActiveCheckInVenueName,
    Guid? ActiveIntentionVenueId,
    string? ActiveIntentionVenueName);

public record UpdatePrivacySettingsRequest(
    bool? IsGhostModeEnabled,
    bool? SharePresenceWithFriends,
    bool? ShareIntentionsWithFriends);

public record SocialTableSummaryDto(
    Guid TableId,
    string Title,
    string? Description,
    DateTimeOffset StartsAtUtc,
    string VenueName,
    string VenueCategory,
    string JoinPolicy,
    bool IsHost,
    string MembershipStatus,
    int Capacity,
    int RequestedCount,
    int AcceptedCount,
    int InvitedCount);

public record SocialTableRequestDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string Status);

public record SocialTableMessageDto(
    Guid MessageId,
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string Body,
    DateTimeOffset SentAtUtc,
    bool IsMine);

public record SocialTableThreadDto(
    SocialTableSummaryDto Table,
    IEnumerable<SocialTableRequestDto> Requests,
    IEnumerable<SocialTableMessageDto> Messages);

public record SendSocialTableMessageRequest(
    string Body);
