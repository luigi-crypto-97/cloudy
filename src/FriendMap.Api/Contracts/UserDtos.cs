namespace FriendMap.Api.Contracts;

public record UserProfileDto(
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
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
    bool CanInviteToTable);

public record SocialActionResultDto(
    string Status,
    string Message);

public record InviteToHostedTableRequest(
    Guid TargetUserId);
