namespace FriendMap.Api.Models;

public class SocialTableMessage : BaseEntity
{
    public Guid SocialTableId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
}
