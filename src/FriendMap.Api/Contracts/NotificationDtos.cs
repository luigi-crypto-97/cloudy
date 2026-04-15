namespace FriendMap.Api.Contracts;

public record RegisterDeviceTokenRequest(
    Guid UserId,
    string Platform,
    string DeviceToken);

public record QueueTestNotificationRequest(
    Guid UserId,
    string Title,
    string Body);
