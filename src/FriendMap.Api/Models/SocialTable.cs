namespace FriendMap.Api.Models;

public class SocialTable : BaseEntity
{
    public Guid VenueId { get; set; }
    public Guid HostUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public int Capacity { get; set; } = 6;
    public string JoinPolicy { get; set; } = "approval";
    public string Status { get; set; } = "open";
}
