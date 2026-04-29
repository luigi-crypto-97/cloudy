namespace FriendMap.Api.Models;

public class UserStoryReaction : BaseEntity
{
    public Guid UserStoryId { get; set; }
    public Guid UserId { get; set; }
    public string ReactionType { get; set; } = "like";
}
