namespace FriendMap.Api.Models;

public class UserStory : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? VenueId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
