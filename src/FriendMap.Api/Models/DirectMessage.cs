namespace FriendMap.Api.Models;

public class DirectMessage : BaseEntity
{
    public Guid ThreadId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset? ReadAtUtc { get; set; }
}
