namespace FriendMap.Api.Models;

public class VenueRating : BaseEntity
{
    public Guid VenueId { get; set; }
    public Guid UserId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public bool IsVerifiedVisit { get; set; }
    public bool IsFlagged { get; set; }
    public DateTimeOffset? FlaggedAtUtc { get; set; }
    public string? FlagReason { get; set; }
}
