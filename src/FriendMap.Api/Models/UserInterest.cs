namespace FriendMap.Api.Models;

public class UserInterest : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public string Tag { get; set; } = string.Empty;
}
