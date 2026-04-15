namespace FriendMap.Api.Models;

public class VenueIntention : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid VenueId { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public string? Note { get; set; }
    public string VisibilityLevel { get; set; } = "friends_or_aggregate";
}
