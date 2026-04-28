namespace FriendMap.Api.Models;

public class UserStoryComment : BaseEntity
{
    public Guid UserStoryId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
}
