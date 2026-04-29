namespace FriendMap.Api.Contracts;

public record RegisterDeviceTokenRequest(
    Guid UserId,
    string Platform,
    string DeviceToken);

public record QueueTestNotificationRequest(
    Guid UserId,
    string Title,
    string Body);

public record NotificationOutboxItemDto(
    Guid Id,
    string Title,
    string Body,
    string Type,
    DateTimeOffset CreatedAtUtc,
    bool IsRead,
    string? DeepLink);

public record NotificationUnreadCountDto(
    int Count);
