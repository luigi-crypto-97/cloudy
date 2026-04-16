namespace FriendMap.Api.Models;

public class DirectMessageThread : BaseEntity
{
    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }
    public DateTimeOffset LastMessageAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
