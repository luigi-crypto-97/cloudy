namespace FriendMap.Api.Models;

public class FeedCardFatigue : BaseEntity
{
    public Guid UserId { get; set; }
    public string CardKey { get; set; } = string.Empty;
    public int SeenCount { get; set; }
    public int DismissedCount { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
    public DateTimeOffset? LastDismissedAtUtc { get; set; }
}
