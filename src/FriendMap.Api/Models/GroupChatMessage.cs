namespace FriendMap.Api.Models;

public class GroupChatMessage : BaseEntity
{
    public Guid GroupChatId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
}
