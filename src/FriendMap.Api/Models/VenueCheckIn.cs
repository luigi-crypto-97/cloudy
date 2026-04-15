namespace FriendMap.Api.Models;

public class VenueCheckIn : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid VenueId { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsManual { get; set; } = true;
    public string VisibilityLevel { get; set; } = "friends_or_aggregate";
}
