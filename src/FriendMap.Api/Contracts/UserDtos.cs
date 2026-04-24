namespace FriendMap.Api.Contracts;

public record UserProfileDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string? Bio,
    int? BirthYear,
    string Gender,
    bool IsFriend,
    string RelationshipStatus,
    int MutualFriendsCount,
    int FriendsCount,
    string PresenceState,
    string StatusLabel,
    string? CurrentVenueName,
    string? CurrentVenueCategory,
    bool CanInviteToTable,
    bool CanMessageDirectly,
    bool CanEditProfile,
    bool IsBlockedByViewer,
    bool HasBlockedViewer,
    IEnumerable<string> Interests);

public record EditableUserProfileDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string? DiscoverablePhone,
    string? DiscoverableEmail,
    string? Bio,
    int? BirthYear,
    string Gender,
    IEnumerable<string> Interests);

public record UserSearchResultDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string RelationshipStatus,
    bool IsBlockedByViewer,
    bool HasBlockedViewer,
    IEnumerable<string> Interests);

public record UpdateMyProfileRequest(
    string? DisplayName,
    string? AvatarUrl,
    string? Bio,
    int? BirthYear,
    string? Gender,
    IEnumerable<string>? Interests);

public record DirectMessageThreadSummaryDto(
    Guid OtherUserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string LastMessagePreview,
    DateTimeOffset LastMessageAtUtc);

public record DirectMessageDto(
    Guid MessageId,
    Guid SenderUserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string Body,
    DateTimeOffset SentAtUtc,
    bool IsMine);

public record DirectMessagePeerDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    bool IsBlockedByViewer,
    bool HasBlockedViewer);

public record DirectMessageThreadDto(
    DirectMessagePeerDto OtherUser,
    IEnumerable<DirectMessageDto> Messages);

public record SendDirectMessageRequest(
    string Body);

public record CreateUserReportRequest(
    Guid ReportedUserId,
    string ReasonCode,
    string? Details);

public record CreateTableReportRequest(
    Guid SocialTableId,
    string ReasonCode,
    string? Details);

public record SocialActionResultDto(
    string Status,
    string Message);

public record InviteToHostedTableRequest(
    Guid TargetUserId);

public record UpdateDiscoveryIdentityRequest(
    string? PhoneNumber,
    string? Email);

public record MatchContactsRequest(
    IEnumerable<string>? Phones,
    IEnumerable<string>? Emails);

public record ContactMatchDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string RelationshipStatus,
    string MatchSource,
    string? CurrentVenueName,
    string? CurrentVenueCategory,
    string StatusLabel);

public record VenueRecapItemDto(
    Guid VenueId,
    string Name,
    string Category,
    int Visits);

public record FriendRecapItemDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    int SharedMoments);

public record UserRecapDto(
    string Period,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    int TotalCheckIns,
    int UniqueVenues,
    int HostedTables,
    int JoinedTables,
    int NightsOutEstimate,
    IEnumerable<VenueRecapItemDto> TopVenues,
    IEnumerable<FriendRecapItemDto> TopPeople);
