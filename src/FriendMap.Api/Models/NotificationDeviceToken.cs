namespace FriendMap.Api.Models;

public class NotificationDeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Platform { get; set; } = "ios";
    public string DeviceToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset LastSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
