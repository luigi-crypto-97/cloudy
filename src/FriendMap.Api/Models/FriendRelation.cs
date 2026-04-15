namespace FriendMap.Api.Models;

public class FriendRelation : BaseEntity
{
    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }
    public string Status { get; set; } = "pending";
}
