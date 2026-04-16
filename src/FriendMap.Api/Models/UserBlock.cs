namespace FriendMap.Api.Models;

public class UserBlock : BaseEntity
{
    public Guid BlockerUserId { get; set; }
    public Guid BlockedUserId { get; set; }
}
