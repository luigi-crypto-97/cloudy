namespace FriendMap.Api.Contracts;

public record CreateGroupChatRequest(
    string Title,
    IEnumerable<Guid> MemberUserIds);

public record GroupChatSummaryDto(
    Guid ChatId,
    string Title,
    string Kind,
    Guid? VenueId,
    string? VenueName,
    int MemberCount,
    string LastMessagePreview,
    DateTimeOffset LastMessageAtUtc,
    int UnreadCount);

public record GroupChatMessageDto(
    Guid MessageId,
    Guid UserId,
    string Nickname,
    string? DisplayName,
    string? AvatarUrl,
    string Body,
    DateTimeOffset SentAtUtc,
    bool IsMine);

public record GroupChatThreadDto(
    GroupChatSummaryDto Chat,
    IEnumerable<GroupChatMessageDto> Messages);

public record SendGroupChatMessageRequest(
    string Body);
