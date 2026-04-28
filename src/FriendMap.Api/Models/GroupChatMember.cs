namespace FriendMap.Api.Models;

public class GroupChatMember : BaseEntity
{
    public Guid GroupChatId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "member";
    public DateTimeOffset? LastReadAtUtc { get; set; }
}
