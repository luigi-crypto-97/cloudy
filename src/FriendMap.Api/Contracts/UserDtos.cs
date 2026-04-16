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
    string? Bio,
    int? BirthYear,
    string Gender,
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
