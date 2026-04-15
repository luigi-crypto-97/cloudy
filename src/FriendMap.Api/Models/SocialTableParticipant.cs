namespace FriendMap.Api.Models;

public class SocialTableParticipant : BaseEntity
{
    public Guid SocialTableId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = "requested";
}
