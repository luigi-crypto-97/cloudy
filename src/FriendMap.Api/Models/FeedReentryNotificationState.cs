namespace FriendMap.Api.Models;

public class FeedReentryNotificationState : BaseEntity
{
    public Guid UserId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string TriggerKey { get; set; } = string.Empty;
    public DateTimeOffset LastSentAtUtc { get; set; }
}
