namespace FriendMap.Api.Models;

public class NotificationOutboxItem : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public string? LastError { get; set; }
}
